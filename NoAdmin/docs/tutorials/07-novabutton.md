# 第七篇：`NovaButtonAttribute` 怎么用

`NovaButtonAttribute` 可以理解成“按钮权限门禁”。
它不是管界面的，而是管方法能不能执行。

## 它做什么

- 检查当前用户有没有权限
- 没权限就直接拦住
- 有权限才继续执行方法

## 适合用在哪

- 编辑
- 删除
- 保存
- 导出
- 打开分配窗口

## 怎么写

直接标在方法上就行：

```csharp
[NovaButton("edit")]
private async Task OpenEditDialog()
{
    editVisible = true;
}
```

## 它和普通按钮的区别

- 普通按钮：点了就执行
- `NovaButtonAttribute`：先检查权限，再执行

## 一句话总结

`NovaButtonAttribute` 就是：**让按钮方法在执行前先过权限检查。**
