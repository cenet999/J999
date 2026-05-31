# 第五篇：认识 `NovaAllocTable`

`NovaAllocTable` 可以理解成“分配关系用的表格组件”。
它不是单纯选数据，而是把一个主对象和它下面的一批子对象绑定起来。

## 它适合做什么

- 给角色分配菜单
- 给租户分配数据库
- 给组织分配成员
- 给主对象挂子对象列表

## 常用参数

- `Item`：当前主对象
- `ChildProperty`：主对象里存子列表的属性名
- `PageSize`：每页条数
- `IsSearchText`：能不能搜
- `OnQuery`：子对象查询逻辑
- `IsNotifyChanged`：保存后要不要通知刷新

## 它怎么工作

流程很简单：

1. 打开弹窗
2. 显示子对象列表
3. 勾选要分配的项
4. 把结果写回主对象
5. 保存

## 它和前两个组件的区别

- `NovaInputTable`：偏输入，适合选对象
- `NovaSelectTable`：偏选择，适合快速挑数据
- `NovaAllocTable`：偏分配，适合保存关系

## 一句话总结

`NovaAllocTable` 就是：**把子对象分配给主对象，并保存关系的组件。**
