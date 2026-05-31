# 第三篇：认识 `NovaInputTable`

`NovaInputTable` 可以理解成“表格版选择器”。
它不是拿来管理列表的，而是拿来在弹窗里选一个对象，或者选多个对象。

## 它适合做什么

- 选择一个关联对象
- 选择多个对象
- 在表单里给主对象挂子对象
- 先搜索，再勾选，最后确认

## 常用参数

### 选中的值

- `Value`：选中的主键
- `Item`：选中的单个对象
- `Items`：选中的多个对象

### 回调

- `ValueChanged`
- `ItemChanged`
- `ItemsChanged`

这些回调用来把选择结果告诉外面。

### 显示和弹窗

- `ModalTitle`：弹窗标题
- `PageSize`：每页条数
- `IsSearchText`：能不能搜文字
- `DialogClassName`：弹窗大小和样式

## 它怎么工作

大致可以理解成三步：

1. 打开弹窗
2. 展示候选列表
3. 选中后把结果回写出去

它只支持单主键实体，这一点很重要。

## 打开弹窗时会做什么

- 先清掉上一次的选择
- 再把当前已有的值重新勾上

所以重新打开时，用户能看到自己之前选了什么。

## 选完后会发生什么

- 单选就回写 `Value` 或 `Item`
- 多选就回写 `Items`
- 同时触发对应的 `Changed` 事件

## 和 `NovaAdminTable` 的关系

可以把它理解成：

- `NovaAdminTable` 负责表格能力
- `NovaInputTable` 负责“从表格里选数据”

## 一句话总结

`NovaInputTable` 就是：**让用户在表格弹窗里快速选对象的组件。**
