# 第一篇：认识 `NovaAdminTable`

`NovaAdminTable` 可以理解成“后台表格的基础模板”。
它不只是显示数据，还顺手把常见的后台功能都包起来了，比如：

- 搜索
- 分页
- 新增、编辑、删除
- 导出
- 选择行
- 表头和行内容自定义

如果你在 NovaAdmin 里看到一个标准的列表页，大概率就是它搭出来的。

## 它适合做什么

- 实体列表页
- 带弹窗编辑的管理页
- 需要筛选、排序、树形展示的复杂表格

## 常用参数

### 数据

- `ItemsSource`：直接用本地列表，不走数据库
- `PageSize`：每页多少条
- `IsQueryString`：是否把筛选条件带到网址里

### 开关

- `IsAdd`：能不能新增
- `IsEdit`：能不能编辑
- `IsRemove`：能不能删除
- `IsView`：能不能查看
- `IsExportExcel`：能不能导出
- `IsSearchText`：能不能搜文字

### 交互

- `IsConfirmEdit`：保存前是否确认
- `IsConfirmRemove`：删除前是否确认
- `IsDrawer`：编辑区用抽屉还是弹窗

## 常用模板

- `TableHeader`：表头
- `TableRow`：每一行显示什么
- `EditTemplate`：新增和编辑表单长什么样

可以简单理解成：

- 你负责写“这一列显示什么”
- `NovaAdminTable` 负责分页、按钮、弹窗和数据操作

## 查询怎么走

查询通常不是写死在组件里，而是交给页面处理。

- `InitQuery`：初始化筛选条件
- `OnQuery`：真正的查询逻辑

这样做的好处是：

- 组件管界面
- 页面管业务

## 编辑怎么走

编辑时，组件会先把当前对象准备好，再交给页面补充处理。

这一步常用于：

- 加载关联数据
- 初始化默认值
- 补齐导航属性

## 保存前校验

保存前可以用 `OnSaving` 做校验。

比如：

```csharp
if (string.IsNullOrWhiteSpace(e.Argument.Content))
{
    e.Cancel = true;
}
```

## 一句话总结

`NovaAdminTable` 就是：**把后台列表页最常见的功能，一次性封装好的表格组件。**
if (string.IsNullOrWhiteSpace(e.Argument.Content))
{
    await MessageService.Error("请录入正文内容！");
    e.Cancel = true;
}
```

这说明 `NovaAdminTable` 的保存流程不是强制你在组件里写死规则，而是提供了一个“保存前拦截点”。

## 7. 选择模式

源码支持两种选择：

- `IsMultiSelect = true`：多选
- `IsSingleSelect = true`：单选

它们会影响：
- 表头是否出现全选框
- 每行是 checkbox 还是 radio
- 行点击行为
- 顶部批量删除按钮是否可用

还有一个很有用的参数：

```csharp
[Parameter] public bool IsAutoSelectParent { get; set; } = false;
```

这个主要服务于树形结构，子节点选中时可以自动影响父节点的选中状态。

## 8. 树形数据支持

源码里对层级数据做了专门处理。

只要 `TItem` 的表关系符合树形引用，组件会进入树形展示逻辑：
- 展开/折叠 caret
- 多层缩进
- 父子联动选择
- 树状列表的加载与显示

这部分逻辑说明 `NovaAdminTable` 不只是普通表格，它还能承担菜单树、分类树、组织树这类页面。

## 9. 列设置和拖拽排序

这是 `NovaAdminTable` 的一个亮点。

当页面里使用了列定义后，组件会自动提供列设置按钮，打开后能：
- 显示/隐藏列
- 拖拽调整顺序
- 固定到左侧或右侧

源码里对应的核心方法包括：
- `AddColumn`
- `RemoveColumn`
- `GetRenderColumns`
- `HandleColumnReorder`
- `ToggleFixed`

这部分用 JS 辅助初始化：

```csharp
await JS.InvokeVoidAsync("novaAdminJS.initializeAdvancedTable", _theadElement, fixedLeft, fixedRight, _objRef, OnDragRow.HasDelegate);
```

所以列拖拽不是纯 Razor 能解决的，组件已经把 JS 交互封装好了。

## 10. 行拖拽排序

源码还支持行拖拽。

对应参数是：

```csharp
[Parameter] public EventCallback<TItem[]> OnDragRow { get; set; }
```

拖拽完成后会调用这个回调，然后重新加载表格。

适合：
- 文章排序
- 菜单排序
- 分类排序
- 自定义优先级排序

## 11. 审核能力

如果实体继承了 `EntityAudited`，组件会自动识别审核相关能力。

它会在表格里显示审核状态列，并且在编辑/保存时根据审核状态限制操作。

同时支持：
- 审核状态展示
- 审核事件回调 `OnAudited`
- 查看审核日志

这也是为什么 `NovaAdminTable` 很适合用在需要审批流的后台。

## 12. 顶部按钮是怎么来的

你在源码里会看到顶部工具栏自动生成：
- 删除
- 编辑
- 添加
- 刷新
- 导出
- 搜索框
- 列设置按钮

这些都来自组件内部 `BuildRenderTree` 的条件渲染。

简单说就是：
- 只要参数允许，就显示
- 只要权限不允许，就隐藏
- 只要页面传了模板，就渲染

这让页面开发者只需要关心业务内容，不需要自己拼一整套表格壳。

## 13. 结合你当前的文章页理解

你打开的 `_Article.razor` 已经把 `NovaAdminTable` 用得很完整了。

这个页面的结构可以拆成三层：

### 列表层

```razor
<NovaAdminTable TItem="Article" Context="item" PageSize="50"  Title="随笔文章" ...>
```

这一层负责表格外壳。

### 查询层

```csharp
async Task InitQuery(NovaAdminQueryInfo e)
```

这一层负责定义筛选项，比如：
- 随笔专栏
- 技术频道
- 随笔类型
- 收藏集
- 标签
- 标题

### 数据层

```csharp
void OnQuery(NovaAdminQueryEventArgs<Article> e)
```

这一层负责真正把 FreeSql 查询拼出来。

### 编辑层

```razor
<EditTemplate>
    ...
</EditTemplate>
```

这一层负责把文章编辑页做成多标签页，分别维护：
- 正文内容
- 浏览/点赞/收藏等统计字段
- 标签和收藏集关系

这就是 `NovaAdminTable` 最有价值的地方：它把“表格外壳”标准化了，让你可以把主要精力放在业务字段和业务逻辑上。

## 14. 一段最小使用方式

如果你想快速写一个页面，最少可以这样理解：

```razor
<NovaAdminTable TItem="Article"
             Title="文章管理"
             InitQuery="InitQuery"
             OnQuery="OnQuery"
             OnEdit="OnEdit"
             OnSaving="OnSaving">
    <TableHeader>
        <th>标题</th>
        <th>创建时间</th>
    </TableHeader>
    <TableRow>
        <td>@item.Title</td>
        <td>@item.CreatedTime</td>
    </TableRow>
    <EditTemplate>
        <input @bind="item.Title" class="form-control" />
    </EditTemplate>
</NovaAdminTable>
```

这样就已经具备一个完整后台列表页的雏形了。

## 15. 这篇教程要记住的重点

最后总结成一句话：

`NovaAdminTable` 不是单纯的表格组件，而是一个围绕实体 CRUD 的后台工作台。

你可以把它理解成：
- 表格壳子
- 查询器
- 编辑器
- 选择器
- 审核器
- 导出器
- 树形列表器

它把通用后台页面最费重复劳动的部分都收拢了。

如果你后面要继续写第二篇，建议可以直接写：
- `NovaAdminTable` 的筛选系统
- `NovaAdminTable` 的编辑弹窗与保存流程
- `NovaAdminTable` 的树形数据支持
- `NovaAdminTable` 的列拖拽与固定列机制
