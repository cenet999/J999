# 第四篇：认识 `NovaSelectTable`

`NovaSelectTable` 可以理解成“更轻一点的表格选择器”。
它和 `NovaInputTable` 很像，但目标更简单：**只负责选择，不负责太多业务处理。**

## 它适合做什么

- 选一个值
- 选一组值
- 保留表格分页、搜索、勾选体验
- 自定义每一行怎么显示

## 常用参数

- `Value`：单选结果
- `Items`：多选结果
- `PageSize`：每页多少条
- `IsSearchText`：能不能搜
- `ChildContent`：每一行怎么显示
- `OnQuery`：查询逻辑交给页面处理

## 它和 `NovaAdminTable` 的关系

`NovaSelectTable` 本质上是把 `NovaAdminTable` 包了一层。

它默认关掉了这些功能：

- 新增
- 编辑
- 删除

意思很简单：**这里只做选择，不做管理。**

## 选择后会发生什么

- 单选时，回写 `Value`
- 多选时，回写 `Items`
- 同时触发对应的回调

## 和 `NovaInputTable` 的区别

- `NovaInputTable` 更偏表单输入
- `NovaSelectTable` 更偏标准选择器

如果只是想“从表格里选一个分类、菜单、字典项”，通常 `NovaSelectTable` 更轻便。

## 一句话总结

`NovaSelectTable` 就是：**基于 `NovaAdminTable` 做出来的轻量选择器。**
