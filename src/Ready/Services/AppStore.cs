using System.Text.Json;
using Ready.Models;

namespace Ready.Services;

public interface IAppStore
{
    AppDocument Document { get; }

    /// <summary>Id of the idea currently open for editing, or null while capturing something new.</summary>
    string? EditingId { get; }

    /// <summary>True when the form content differs from what was last saved.</summary>
    bool HasUnsavedChanges { get; }

    /// <summary>True when the quick-capture form holds something worth guarding.</summary>
    bool HasFormContent { get; }

    /// <summary>True when the last clear can still be rolled back.</summary>
    bool CanUndoClear { get; }

    event Action? Changed;

    Task LoadAsync();
    Task SaveDraftAsync();
    Task<bool> CreateAsync();
    Task<IdeaRecord?> SaveAsync(string id);
    Task DeleteAsync(string id);
    Task TogglePinAsync(string id);
    Task DuplicateAsync(IdeaRecord idea);

    /// <summary>Opens an existing idea in the capture form, remembering the previous form.</summary>
    void StartEdit(IdeaRecord idea);

    /// <summary>Leaves the editor and restores the form that was open before editing.</summary>
    void CancelEdit();

    /// <summary>Empties the form and leaves the editor.</summary>
    void BeginNew();

    void Clear();
    bool UndoClear();
    Task UpdateSettingsAsync();
    string ExportJson();
    Task<ImportResult> ImportAsync(string json, bool replace = false);
}

public sealed record ImportResult(bool Success, string Message, int Count = 0);

public sealed class AppStore : IAppStore
{
    private readonly string _path;

    // 所有落盘都排队走这一把锁。以前没有这把锁：改一个开关（UpdateSettingsAsync）和
    // 输入触发自动保存（SaveDraftAsync）可能同时进入 PersistAsync，两条写共用同一个
    // ".tmp" 文件名，后一个 File.Move 可能搬走前一个刚写了一半的内容。
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    private IdeaData? _persistedDraft;
    private IdeaData? _formBeforeEdit;
    private IdeaData? _undoBuffer;
    private string _savedSignature = "";
    private string _editSignature = "";

    public AppStore(string path) { _path = path; }

    public AppDocument Document { get; private set; } = new();
    public string? EditingId { get; private set; }

    public bool HasUnsavedChanges => DraftSignature() != (EditingId is null ? _savedSignature : _editSignature);

    public bool HasFormContent => Document.Draft.Summary.Trim().Length > 0 || IdeaData.TagFields.Any(f => Document.Draft.TagsFor(f)!.Count > 0);

    public bool CanUndoClear => _undoBuffer is not null;

    public event Action? Changed;

    public async Task LoadAsync()
    {
        var migrated = false;
        try
        {
            if (File.Exists(_path))
                Document = ReadyJson.Deserialize<AppDocument>(await File.ReadAllTextAsync(_path)) ?? new();
            Document.Ideas ??= []; Document.Draft ??= new(); Document.Settings ??= new(); Document.CustomTags ??= [];
            foreach (var idea in Document.Ideas) idea.Normalize();
            Document.Draft.Normalize();
            Document.Settings.Normalize();
            MigrateLegacyRoles();
            migrated = MigrateClosePrompt();
            ResetSavedDraft();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Keep the damaged file around instead of silently overwriting it.
            if (File.Exists(_path)) File.Copy(_path, _path + ".broken", true);
            Document = new(); ResetSavedDraft();
        }

        // 迁移必须落盘：原生层在关闭窗口时直接读文件判断该退到托盘还是退出，
        // 只改内存的话，下次点关闭仍然会按旧的「直接退出」执行，页面根本没机会问。
        if (migrated) await PersistAsync();

        Notify();
    }

    /// <summary>
    /// v1 在按钮按下的那一刻就把答案写进设置——对话框里没有「不再询问」这回事，所以
    /// 存下来的 tray/exit 并不是用户明确表示「以后别问我」。v2 起只有勾选才记住，
    /// 老文件因此退回「每次询问」，让用户在带复选框的对话框里重新决定一次。
    /// </summary>
    private bool MigrateClosePrompt()
    {
        if (Document.SchemaVersion >= 2) return false;
        Document.Settings.CloseBehavior = CloseBehavior.Ask;
        Document.SchemaVersion = 2;
        return true;
    }

    /// <summary>Persists the in-progress form. Skipped while editing an idea.</summary>
    public async Task SaveDraftAsync()
    {
        if (EditingId is not null) return;
        ResetSavedDraft();
        await PersistAsync();
    }

    /// <summary>
    /// 「先改内存、再落盘、失败就整体退回」的一份快照。以前写失败只是静默地留下
    /// 内存里的改动，界面照常显示成功，重启之后才发现磁盘还是旧的。
    /// </summary>
    private sealed record Snapshot(
        List<IdeaRecord> Ideas, IdeaDraft Draft, string? EditingId,
        IdeaData? UndoBuffer, IdeaData? FormBeforeEdit,
        string SavedSignature, string EditSignature,
        IdeaData? PersistedDraft, Dictionary<string, List<string>> CustomTags)
    {
        public static Snapshot Of(AppStore store) => new(
            [.. store.Document.Ideas.Select(RecordCopy)],
            DraftCopy(store.Document.Draft),
            store.EditingId,
            store._undoBuffer?.Copy(),
            store._formBeforeEdit?.Copy(),
            store._savedSignature,
            store._editSignature,
            store._persistedDraft?.Copy(),
            store.Document.CustomTags.ToDictionary(pair => pair.Key, pair => new List<string>(pair.Value)));

        // 快照必须是深拷贝：保存是就地改写记录对象（Draft.CopyTo(idea)），
        // 只复制列表的话，回滚拿回来的仍是那份已经被改过的记录。
        static IdeaRecord RecordCopy(IdeaRecord source)
        {
            var copy = new IdeaRecord
            {
                Id = source.Id,
                CreatedAt = source.CreatedAt,
                UpdatedAt = source.UpdatedAt,
                Pinned = source.Pinned
            };
            source.CopyTo(copy);
            return copy;
        }

        // IdeaData.Copy() 返回的是基类实例，不能直接强转成 IdeaDraft；这里显式搬一次。
        static IdeaDraft DraftCopy(IdeaDraft source)
        {
            var copy = new IdeaDraft();
            source.CopyTo(copy);
            return copy;
        }

        public void Restore(AppStore store)
        {
            store.Document.Ideas = Ideas;
            store.Document.Draft = Draft;
            store.EditingId = EditingId;
            store._undoBuffer = UndoBuffer;
            store._formBeforeEdit = FormBeforeEdit;
            store._savedSignature = SavedSignature;
            store._editSignature = EditSignature;
            store._persistedDraft = PersistedDraft;
            store.Document.CustomTags = CustomTags;
        }
    }

    public async Task<bool> CreateAsync()
    {
        Document.Draft.Normalize();
        if (Document.Draft.Summary.Length == 0) return false;
        var snapshot = Snapshot.Of(this);
        var idea = IdeaRecord.FromDraft(Document.Draft);
        Document.Ideas.Insert(0, idea);
        Document.Draft = new();
        _undoBuffer = null; _formBeforeEdit = null; EditingId = null;
        ResetSavedDraft();
        if (await PersistAsync()) return true;
        snapshot.Restore(this);
        Notify();
        return false;
    }

    public async Task<IdeaRecord?> SaveAsync(string id)
    {
        Document.Draft.Normalize();
        if (Document.Draft.Summary.Length == 0) return null;
        var idea = Document.Ideas.FirstOrDefault(i => i.Id == id);
        if (idea is null) return null;
        var snapshot = Snapshot.Of(this);
        Document.Draft.CopyTo(idea);
        idea.Normalize();
        idea.UpdatedAt = DateTimeOffset.Now;
        Document.Draft = new();
        // 表单已经清空，撤回没有可回退的对象了，留着只会把旧内容灌进空表单。
        _undoBuffer = null; _formBeforeEdit = null; EditingId = null;
        ResetSavedDraft();
        if (await PersistAsync()) return idea;
        snapshot.Restore(this);
        Notify();
        return null;
    }

    public async Task DeleteAsync(string id)
    {
        var snapshot = Snapshot.Of(this);
        if (Document.Ideas.RemoveAll(i => i.Id == id) == 0) return;
        if (EditingId == id) CancelEdit();
        if (await PersistAsync()) return;
        snapshot.Restore(this);
        Notify();
    }

    public async Task TogglePinAsync(string id)
    {
        var idea = Document.Ideas.FirstOrDefault(i => i.Id == id);
        if (idea is null) return;
        var snapshot = Snapshot.Of(this);
        idea.Pinned = !idea.Pinned;
        if (await PersistAsync()) return;
        snapshot.Restore(this);
        Notify();
    }

    public async Task DuplicateAsync(IdeaRecord idea)
    {
        var snapshot = Snapshot.Of(this);
        var copy = IdeaRecord.From(idea);
        copy.Id = Guid.NewGuid().ToString("N");
        copy.CreatedAt = copy.UpdatedAt = DateTimeOffset.Now;
        copy.Summary += "（副本）";
        copy.Pinned = false;
        Document.Ideas.Insert(0, copy);
        if (await PersistAsync()) return;
        snapshot.Restore(this);
        Notify();
    }

    public void StartEdit(IdeaRecord idea)
    {
        if (EditingId is not null) CancelEdit();
        _formBeforeEdit = Document.Draft.Copy();
        idea.CopyTo(Document.Draft);
        EditingId = idea.Id;
        _editSignature = DraftSignature();
        Notify();
    }

    public void CancelEdit()
    {
        if (EditingId is null) return;
        _formBeforeEdit?.CopyTo(Document.Draft);
        _formBeforeEdit = null; EditingId = null;
        _editSignature = "";
        Notify();
    }

    public void BeginNew()
    {
        // 新表单不该继承上一份内容的「撤回」：否则空表单上会出现撤回按钮，
        // 点一下就把刚被清除的旧内容灌回来。
        _undoBuffer = null; _formBeforeEdit = null; EditingId = null;
        _editSignature = "";
        Document.Draft = new();
        ResetSavedDraft();
        Notify();
    }

    public void Clear() { _undoBuffer = Document.Draft.Copy(); Document.Draft = new(); Notify(); }

    public bool UndoClear()
    {
        if (_undoBuffer is null) return false;
        _undoBuffer.CopyTo(Document.Draft); _undoBuffer = null;
        if (EditingId is null) ResetSavedDraft(); else _editSignature = DraftSignature();
        Notify();
        return true;
    }

    public async Task UpdateSettingsAsync() => await PersistAsync();

    public string ExportJson() => ReadyJson.Serialize(new IdeaExport { Ideas = Document.Ideas });

    public async Task<ImportResult> ImportAsync(string json, bool replace = false)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new(false, "备份必须是 JSON 对象。 Ready! 不会覆盖现有数据。");
            var root = doc.RootElement;
            if (!root.TryGetProperty("Version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
                return new(false, "备份版本不受支持，请选择 Ready! v1 导出的文件。");
            if (!root.TryGetProperty("Ideas", out var ideas) || ideas.ValueKind != JsonValueKind.Array)
                return new(false, "备份中缺少构思列表。");
            var imported = ideas.Deserialize(ReadyJson.Info<List<IdeaRecord>>()) ?? [];
            if (imported.Any(i => string.IsNullOrWhiteSpace(i.Summary))) return new(false, "备份包含没有一句话总结的构思，未导入。");
            var existingIds = Document.Ideas.Select(i => i.Id).ToHashSet();
            foreach (var idea in imported)
            {
                idea.Normalize();
                if (existingIds.Contains(idea.Id)) idea.Id = Guid.NewGuid().ToString("N");
                existingIds.Add(idea.Id);
            }
            var snapshot = Snapshot.Of(this);
            if (replace) Document.Ideas = imported;
            else Document.Ideas.AddRange(imported);
            if (!await PersistAsync())
            {
                // 导入失败不能留下「内存里已经有了」的半截状态：用户会以为备份成了。
                snapshot.Restore(this);
                Notify();
                return new(false, "备份无法写入本机文件，现有数据未改变。");
            }
            return new(true, $"已导入 {imported.Count} 个构思。", imported.Count);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { return new(false, "无法读取这个备份，请检查 JSON 格式与字段。现有数据未改变。"); }
    }

    private string DraftSignature() => ReadyJson.Serialize(Document.Draft);

    /// <summary>
    /// The role list moved from story archetypes to on-screen character types. Anything a
    /// user picked from the old list is preserved as a custom tag so no idea loses a role,
    /// and the picker keeps showing both the new defaults and those old choices.
    /// </summary>
    private void MigrateLegacyRoles()
    {
        var known = TagCatalog.All("roles", Document.CustomTags).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var extras = new List<string>();
        var touched = false;

        void Collect(IEnumerable<string> roles)
        {
            foreach (var role in roles)
            {
                if (role.Length == 0 || known.Contains(role)) continue;
                known.Add(role); extras.Add(role); touched = true;
            }
        }

        foreach (var idea in Document.Ideas) Collect(idea.Roles);
        Collect(Document.Draft.Roles);
        if (!touched) return;

        if (!Document.CustomTags.TryGetValue("roles", out var existing) || existing is null)
            Document.CustomTags["roles"] = existing = [];
        foreach (var role in extras)
            if (!existing.Contains(role, StringComparer.CurrentCultureIgnoreCase)) existing.Add(role);
    }

    private void ResetSavedDraft()
    {
        _persistedDraft = new IdeaDraft();
        Document.Draft.CopyTo(_persistedDraft);
        _savedSignature = DraftSignature();
    }

    private async Task<bool> PersistAsync()
    {
        // The draft slot only ever receives an explicitly saved capture form.
        // While editing, or with autosave off, the file keeps the last saved draft.
        var outgoing = Document;
        if (EditingId is not null || (!Document.Settings.AutosaveDraft && _persistedDraft is not null))
        {
            outgoing = new AppDocument
            {
                SchemaVersion = Document.SchemaVersion,
                Ideas = Document.Ideas,
                Draft = (IdeaDraft)(_persistedDraft ?? new IdeaDraft()),
                CustomTags = Document.CustomTags,
                Settings = Document.Settings
            };
        }
        // 序列化放在锁外面：它是纯内存操作，没必要让第二个写等它。
        var payload = ReadyJson.Serialize(outgoing);
        var temporary = _path + ".tmp";

        await _writeGate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(temporary, payload);
            File.Move(temporary, _path, true);
            Notify();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 别把半截文件留在目录里：残留本身就会误导排查。
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
            return false;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void Notify() => Changed?.Invoke();
}
