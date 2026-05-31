# 第八篇：`NovaInputFile` 怎么用

`NovaInputFile` 用来做文件选择。
它把“输入框 + 选择按钮 + 文件弹窗”封装成一个组件，页面里可以直接用。

## 它做什么

- 显示当前选中的文件地址
- 点击按钮打开文件选择弹窗
- 选中后把文件地址回填到绑定值

## 怎么写

直接像普通组件一样用：

```razor
<NovaInputFile @bind-Value="selectedFileUrl" ModalTitle="选择一个文件" />
```

## 常见参数

- `@bind-Value`：绑定选中的文件地址
- `ModalTitle`：弹窗标题

## 适合用在哪

- 图片选择
- 附件选择
- 文件路径回填
- 需要从后台文件库里挑一个地址的场景

## 一句话总结

`NovaInputFile` 就是：**把文件选择封装成一个可直接复用的输入组件。**
