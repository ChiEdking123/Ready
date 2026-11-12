using Ready.Models;
using Ready.Services;
using Xunit;

namespace Ready.Tests;

public sealed class AppStoreTests : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "ready-tests-" + Guid.NewGuid().ToString("N"));
    readonly string path;
    AppStore store;
    public AppStoreTests() { path = Path.Combine(directory, "data.json"); store = new AppStore(path); }

    [Fact]
    public async Task Create_requires_a_summary_and_preserves_tags()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "   ";
        Assert.False(await store.CreateAsync());
        store.Document.Draft.Summary = " 在时间里经营食堂 ";
        store.Document.Draft.Genres.AddRange(["模拟经营", "解谜"]);
        store.Document.Draft.Roles.AddRange(["猫咪店主", "夜班客人"]);
        Assert.True(await store.CreateAsync());
        Assert.Equal("", store.Document.Draft.Summary);
        Assert.Equal("在时间里经营食堂", store.Document.Ideas[0].Summary);
        Assert.Equal(["猫咪店主", "夜班客人"], store.Document.Ideas[0].Roles);
    }

    [Fact]
    public async Task Create_persists_and_reloads()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "猫咪的时间食堂";
        store.Document.Draft.Roles.AddRange(["猫咪店主", "夜班客人"]);
        store.Document.CustomTags["genres"] = ["治愈模拟"];
        Assert.True(await store.CreateAsync());
        store = new AppStore(path); await store.LoadAsync();
        var idea = Assert.Single(store.Document.Ideas);
        Assert.Equal("猫咪的时间食堂", idea.Summary);
        Assert.Equal("猫咪店主", idea.Roles[0]);
        Assert.Equal("治愈模拟", store.Document.CustomTags["genres"][0]);
    }

    [Fact]
    public async Task Draft_autosaves_independently_from_ideas()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "还没决定要不要做的游戏";
        await store.SaveDraftAsync();
        Assert.Empty(store.Document.Ideas);
        store = new AppStore(path); await store.LoadAsync();
        Assert.Equal("还没决定要不要做的游戏", store.Document.Draft.Summary);
    }

    [Fact]
    public async Task Edit_save_does_not_create_a_duplicate()
    {
        await store.LoadAsync(); store.Document.Draft.Summary = "第一个版本"; await store.CreateAsync();
        var id = store.Document.Ideas[0].Id;
        store.StartEdit(store.Document.Ideas[0]);
        Assert.Equal(id, store.EditingId);
        store.Document.Draft.Summary = "第二个版本";
        Assert.NotNull(await store.SaveAsync(id));
        Assert.Null(store.EditingId);
        Assert.Single(store.Document.Ideas); Assert.Equal("第二个版本", store.Document.Ideas[0].Summary);
    }

    [Fact]
    public async Task Editing_a_second_idea_restores_the_unsaved_form_of_the_first()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "旧草稿"; await store.SaveDraftAsync();
        store.Document.Draft.Summary = "新草稿";
        store.Document.Draft.Summary = "构思甲"; await store.CreateAsync();
        store.Document.Draft.Summary = "构思乙"; await store.CreateAsync();
        var first = store.Document.Ideas.Single(i => i.Summary == "构思甲");
        var second = store.Document.Ideas.Single(i => i.Summary == "构思乙");

        store.StartEdit(first);
        store.Document.Draft.Summary = "构思甲 · 未保存修改";
        store.StartEdit(second);

        Assert.Equal("构思乙", store.Document.Draft.Summary);
        Assert.Equal(second.Id, store.EditingId);
        Assert.DoesNotContain(store.Document.Ideas, i => i.Summary.Contains("未保存修改"));
    }

    [Fact]
    public async Task Pending_edit_is_not_written_to_the_draft_slot()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "构思本身"; await store.CreateAsync();
        var idea = store.Document.Ideas[0];

        store.StartEdit(idea);
        store.Document.Draft.Summary = "正在改，但还没保存";
        await store.SaveDraftAsync();
        await store.TogglePinAsync(idea.Id);

        store = new AppStore(path); await store.LoadAsync();
        Assert.Equal("", store.Document.Draft.Summary);
        Assert.Equal("构思本身", store.Document.Ideas[0].Summary);
    }

    [Fact]
    public async Task CancelEdit_restores_the_draft_that_existed_before_editing()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "已创建的构思"; await store.CreateAsync();
        var idea = store.Document.Ideas[0];
        store.Document.Draft.Summary = "手头的草稿";
        store.Document.Draft.Genres.Add("解谜");

        store.StartEdit(idea);
        Assert.Equal("已创建的构思", store.Document.Draft.Summary);
        store.Document.Draft.Summary = "改到一半";
        store.CancelEdit();

        Assert.Null(store.EditingId);
        Assert.Equal("手头的草稿", store.Document.Draft.Summary);
        Assert.Contains("解谜", store.Document.Draft.Genres);
    }

    [Fact]
    public async Task Deleting_the_idea_being_edited_clears_the_editor()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "会被删除"; await store.CreateAsync();
        var idea = store.Document.Ideas[0];
        store.StartEdit(idea);
        await store.DeleteAsync(idea.Id);
        Assert.Null(store.EditingId);
        Assert.Empty(store.Document.Ideas);
    }

    [Fact]
    public async Task Unsaved_changes_reset_after_saving()
    {
        await store.LoadAsync();
        Assert.False(store.HasUnsavedChanges);
        store.Document.Draft.Summary = "写了一点";
        Assert.True(store.HasUnsavedChanges);
        await store.SaveDraftAsync();
        Assert.False(store.HasUnsavedChanges);
    }

    [Fact]
    public async Task Untouched_edit_does_not_report_pending_changes()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "原样"; await store.CreateAsync();
        store.StartEdit(store.Document.Ideas[0]);
        Assert.False(store.HasUnsavedChanges);
        store.Document.Draft.Summary = "改了";
        Assert.True(store.HasUnsavedChanges);
    }

    [Fact]
    public async Task Disabling_autosave_keeps_unsaved_form_out_of_the_file()
    {
        await store.LoadAsync();
        store.Document.Settings.AutosaveDraft = false;
        store.Document.Draft.Summary = "不想被自动保存"; await store.CreateAsync();
        store.Document.Draft.Summary = "半成品";
        await store.TogglePinAsync(store.Document.Ideas[0].Id);

        store = new AppStore(path); await store.LoadAsync();
        Assert.Equal("", store.Document.Draft.Summary);
        Assert.Single(store.Document.Ideas);
    }

    [Fact]
    public async Task Clear_and_undo_restore_an_exact_copy()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "一个差点被清空的构思";
        store.Document.Draft.Genres = ["解谜", "冒险"];
        store.Clear(); Assert.Equal("", store.Document.Draft.Summary);
        Assert.True(store.UndoClear());
        Assert.Equal("一个差点被清空的构思", store.Document.Draft.Summary);
        Assert.Equal(["解谜", "冒险"], store.Document.Draft.Genres);
        Assert.False(store.UndoClear());
    }

    [Fact]
    public async Task Pin_delete_and_duplicate_manage_multiple_ideas()
    {
        await store.LoadAsync();
        foreach (var title in new[] { "一", "二", "三" }) { store.Document.Draft.Summary = title; await store.CreateAsync(); }
        var idea = store.Document.Ideas[1]; await store.TogglePinAsync(idea.Id); Assert.True(idea.Pinned);
        await store.DuplicateAsync(idea);
        Assert.Equal(4, store.Document.Ideas.Count);
        Assert.NotEqual(idea.Id, store.Document.Ideas[0].Id);
        Assert.Contains("（副本）", store.Document.Ideas[0].Summary);
        await store.DeleteAsync(idea.Id); Assert.DoesNotContain(store.Document.Ideas, i => i.Id == idea.Id);
    }

    [Fact]
    public async Task Duplicate_is_persisted_immediately()
    {
        await store.LoadAsync(); store.Document.Draft.Summary = "被复制的构思"; await store.CreateAsync();
        await store.DuplicateAsync(store.Document.Ideas[0]);
        store = new AppStore(path); await store.LoadAsync();
        Assert.Equal(2, store.Document.Ideas.Count);
    }

    [Fact]
    public async Task Export_import_round_trips_without_colliding_ids()
    {
        await store.LoadAsync(); store.Document.Draft.Summary = "备份测试"; await store.CreateAsync();
        var json = store.ExportJson();
        var result = await store.ImportAsync(json);
        Assert.True(result.Success); Assert.Equal(2, store.Document.Ideas.Count);
        Assert.Equal(2, store.Document.Ideas.Select(i => i.Id).Distinct().Count());
    }

    [Fact]
    public async Task Imported_ideas_keep_their_tag_order()
    {
        await store.LoadAsync();
        var json = """
            {"Version":1,"Ideas":[{"Summary":"导入的构思","Roles":["侦探","助手"],"Genres":["解谜","叙事"]}]}
            """;
        Assert.True((await store.ImportAsync(json)).Success);
        Assert.Equal(["侦探", "助手"], store.Document.Ideas[0].Roles);
        Assert.Equal(["解谜", "叙事"], store.Document.Ideas[0].Genres);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"Version\":2,\"Ideas\":[]}")]
    [InlineData("{\"Version\":1}")]
    [InlineData("{\"Version\":1,\"Ideas\":[{\"Summary\":\"\"}]}")]
    public async Task Invalid_import_does_not_destroy_existing_data(string json)
    {
        await store.LoadAsync(); store.Document.Draft.Summary = "仍然在这里"; await store.CreateAsync();
        var result = await store.ImportAsync(json);
        Assert.False(result.Success); Assert.Single(store.Document.Ideas); Assert.Equal("仍然在这里", store.Document.Ideas[0].Summary);
    }

    [Fact]
    public async Task Corrupt_local_file_is_backed_up_instead_of_throwing()
    {
        Directory.CreateDirectory(directory); await File.WriteAllTextAsync(path, "{oops", TestContext.Current.CancellationToken);
        await store.LoadAsync(); Assert.Empty(store.Document.Ideas); Assert.True(File.Exists(path + ".broken"));
    }

    [Fact]
    public async Task Missing_optional_collections_are_repaired_on_load()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """{"SchemaVersion":1,"Ideas":[{"Summary":"缺字段的构思"}]}""", TestContext.Current.CancellationToken);
        await store.LoadAsync();
        Assert.Single(store.Document.Ideas);
        Assert.NotNull(store.Document.Ideas[0].Roles);
        Assert.NotNull(store.Document.Draft);
        Assert.NotNull(store.Document.Settings);
    }

    [Fact]
    public void Tag_catalog_has_custom_tags_after_defaults()
    {
        var tags = new Dictionary<string, List<string>> { ["genres"] = ["烹饪"] };
        var all = TagCatalog.All("genres", tags);
        Assert.Equal("烹饪", all[^1]); Assert.Contains("动作", all);
    }

    [Fact]
    public void Normalize_removes_duplicates_without_reordering_roles()
    {
        var data = new IdeaData { Roles = ["主角", " 队友 ", "主角", ""] };
        data.Normalize(); Assert.Equal(["主角", "队友"], data.Roles);
    }

    [Fact]
    public async Task Missing_ui_scale_falls_back_to_100()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """{"SchemaVersion":1,"Ideas":[],"Settings":{"Theme":"dark"}}""", TestContext.Current.CancellationToken);
        await store.LoadAsync();
        Assert.Equal(100, store.Document.Settings.UiScalePercent);
        Assert.Equal("dark", store.Document.Settings.Theme);
    }

    [Theory]
    [InlineData(10, 85)]
    [InlineData(300, 140)]
    [InlineData(118, 112)]
    public async Task Out_of_range_ui_scale_is_snapped_to_a_step(int stored, int expected)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, "{\"SchemaVersion\":1,\"Ideas\":[],\"Settings\":{\"UiScalePercent\":" + stored + "}}", TestContext.Current.CancellationToken);
        await store.LoadAsync();
        Assert.Equal(expected, store.Document.Settings.UiScalePercent);
    }

    [Fact]
    public async Task Ui_scale_setting_round_trips()
    {
        store.Document.Settings.UiScalePercent = 125;
        await store.UpdateSettingsAsync();

        var reopened = new AppStore(path);
        await reopened.LoadAsync();
        Assert.Equal(125, reopened.Document.Settings.UiScalePercent);
    }

    [Theory]
    [InlineData(85, -1, 85)]
    [InlineData(140, 1, 140)]
    [InlineData(100, 1, 112)]
    [InlineData(112, -1, 100)]
    public void Ui_scale_nudge_steps_and_stops_at_the_edges(int current, int direction, int expected)
    {
        Assert.Equal(expected, UiScale.Nudge(current, direction));
    }

    [Fact]
    public void Role_defaults_are_on_screen_character_types()
    {
        var roles = TagCatalog.All("roles", new Dictionary<string, List<string>>());
        Assert.Contains("成年男性", roles);
        Assert.Contains("少女", roles);
        Assert.Contains("机器人", roles);
        Assert.DoesNotContain("普通人", roles);
        Assert.DoesNotContain("英雄", roles);
    }

    [Fact]
    public void Audience_defaults_are_player_motivation_types()
    {
        var audiences = TagCatalog.All("audiences", new Dictionary<string, List<string>>());
        Assert.Equal(["休闲型玩家", "成就型玩家", "探索型玩家", "社交型玩家", "沉浸型玩家"], audiences);
        Assert.DoesNotContain("核心玩家", audiences);
        Assert.DoesNotContain("速通玩家", audiences);
    }

    [Fact]
    public void Non_goals_are_a_tag_field_with_defaults()
    {
        Assert.Contains("nongoals", IdeaData.TagFields);
        var nonGoals = TagCatalog.All("nongoals", new Dictionary<string, List<string>>());
        Assert.NotEmpty(nonGoals);
        Assert.Contains("不做多人联机", nonGoals);

        // TagsFor 必须指向同一份列表，否则选择、归一化与持久化会各写各的。
        var data = new IdeaData();
        Assert.Same(data.NonGoals, data.TagsFor("nongoals"));
    }

    [Fact]
    public async Task Non_goals_round_trip_through_save_and_reload()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "有边界的构思";
        store.Document.Draft.NonGoals.AddRange(["不做多人联机", "不做开放世界"]);
        Assert.True(await store.CreateAsync());

        store = new AppStore(path); await store.LoadAsync();
        Assert.Equal(["不做多人联机", "不做开放世界"], store.Document.Ideas[0].NonGoals);
    }

    [Fact]
    public async Task Legacy_audience_values_survive_on_existing_ideas()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """
            {"SchemaVersion":2,"Ideas":[{"Summary":"旧受众的构思","Audiences":["核心玩家"]}],"Settings":{}}
            """, TestContext.Current.CancellationToken);
        await store.LoadAsync();

        Assert.Equal(["核心玩家"], store.Document.Ideas[0].Audiences);
        Assert.DoesNotContain("核心玩家", TagCatalog.All("audiences", store.Document.CustomTags));
    }

    [Fact]
    public async Task Legacy_roles_are_kept_as_custom_tags()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """
            {"SchemaVersion":1,"Ideas":[],"Draft":{"Summary":"","Roles":["普通人","少女"]},"Settings":{}}
            """, TestContext.Current.CancellationToken);
        await store.LoadAsync();

        var roles = store.Document.Draft.Roles;
        Assert.Contains("少女", roles);
        Assert.True(store.Document.CustomTags.TryGetValue("roles", out var custom));
        Assert.Contains("普通人", custom);

        var all = TagCatalog.All("roles", store.Document.CustomTags);
        Assert.Contains("普通人", all);
        Assert.Contains("少女", all);
    }

    [Theory]
    [InlineData("weird", "ask")]
    [InlineData("", "ask")]
    [InlineData(null, "ask")]
    [InlineData("tray", "tray")]
    [InlineData("exit", "exit")]
    public void Close_behavior_normalizes_unknown_values(string? stored, string expected)
    {
        Assert.Equal(expected, Ready.Models.CloseBehavior.Normalize(stored));
    }

    [Fact]
    public async Task Close_behavior_round_trips()
    {
        store.Document.Settings.CloseBehavior = Ready.Models.CloseBehavior.Tray;
        await store.UpdateSettingsAsync();

        var reopened = new AppStore(path);
        await reopened.LoadAsync();
        Assert.Equal(Ready.Models.CloseBehavior.Tray, reopened.Document.Settings.CloseBehavior);
    }

    [Fact]
    public async Task Concurrent_writes_do_not_corrupt_the_file_or_leave_a_temporary()
    {
        await store.LoadAsync();

        // 自动保存与设置写入走的是同一条落盘路径：以前两条写共用同一个 ".tmp"，
        // 后一个 File.Move 可能搬走前一个刚写了一半的内容。
        store.Document.Draft.Summary = "并发写入的构思";
        var tasks = new[]
        {
            store.SaveDraftAsync(),
            store.UpdateSettingsAsync(),
            store.SaveDraftAsync()
        };
        await Task.WhenAll(tasks);

        Assert.False(File.Exists(path + ".tmp"));

        var reopened = new AppStore(path);
        await reopened.LoadAsync();
        Assert.Equal("并发写入的构思", reopened.Document.Draft.Summary);
    }

    [Fact]
    public async Task Create_reports_failure_and_rolls_back_when_the_file_cannot_be_written()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "写不进去的构思";

        // 用一个同名文件占住数据目录的位置：Directory.CreateDirectory 会抛 IOException，
        // 这正是磁盘满、权限丢失时需要的那种「落盘失败」。
        var blocked = Path.Combine(directory, "blocked");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(blocked, "not a directory", TestContext.Current.CancellationToken);
        var failing = new AppStore(Path.Combine(blocked, "data.json"));
        await failing.LoadAsync();
        failing.Document.Draft.Summary = "写不进去的构思";

        Assert.False(await failing.CreateAsync());

        // 内存必须回到落盘之前：否则界面会显示已创建，重启后却什么都没有。
        Assert.Empty(failing.Document.Ideas);
        Assert.Equal("写不进去的构思", failing.Document.Draft.Summary);
    }

    [Fact]
    public async Task Save_reports_failure_and_keeps_the_previous_record()
    {
        await store.LoadAsync();
        store.Document.Draft.Summary = "原始标题";
        Assert.True(await store.CreateAsync());
        var id = store.Document.Ideas[0].Id;

        var blocked = Path.Combine(directory, "blocked-save");
        await File.WriteAllTextAsync(blocked, "not a directory", TestContext.Current.CancellationToken);
        var failing = new AppStore(Path.Combine(blocked, "data.json"));
        await failing.LoadAsync();
        failing.Document.Ideas.Add(IdeaRecord.FromDraft(new IdeaDraft { Summary = "原始标题" }));
        var target = failing.Document.Ideas[0];
        failing.StartEdit(target);
        failing.Document.Draft.Summary = "改过的标题";

        Assert.Null(await failing.SaveAsync(target.Id));
        // 内存里那条记录必须保持旧标题，表单也不能被清空。
        Assert.Equal("原始标题", failing.Document.Ideas[0].Summary);
        Assert.Equal("改过的标题", failing.Document.Draft.Summary);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
