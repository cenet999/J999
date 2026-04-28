# MS 游戏 API 配置信息

## 账号信息

| 项目       | 值                    |
|------------|------------------------|
| 账号       | levers990              |
| 钱包余额   | 1000.00                |
| 通用分余额 | 96238.60               |
| 前缀       | evl                    |
| ApiKey     | FJdifHOvOSErvpwU73VndWsPJvg4kOtx |

---

## API 地址配置

### 主线路 API

- `https://apis.ms-bet.com`
- `https://apis.msh.best`
- `https://apis.msh.cool`

### 备用 API

- `https://apis.msgm01.com`
- `https://apis.msgm02.com`
- `https://apis.msgm03.com`
- `https://apis.msgm04.com`
- `https://apis.msgm05.com`
- `https://apis.msgm06.com`
- `https://apis.msgm07.com`
- `https://apis.msgm08.com`
- `https://apis.msgm09.com`
- `https://apis.msgm10.com`

### 后台更换域名方法

1. **接口功能** → 接口管理 → 基础域名
2. **系统设置** → 接口设置 → API 接口地址

---

## 通用规则

- **请求方式**：统一使用 `POST`
- **Content-Type**：`application/x-www-form-urlencoded`

### 状态码 (Code)

| Code | 描述                        | 说明               |
|------|-----------------------------|--------------------|
| 0    | 成功                        | 请求成功           |
| -1   | 未知错误                    | 见返回的错误信息   |
| 10   | api_code error              | 一般为 api_code 错误 |
| 11   | Merchant api_code error     | 一般为 api_code 错误 |
| 33   | Member already exists       | 会员已存在         |
| 34   | No user exists, please register | 会员未注册     |
| 35   | Order No. already exists    | 订单号重复         |
| 56   | Insufficient merchant balance | 商户额度不足    |
| 999  | Merchant or key error       | 商户或 key 错误    |
| 1000 | Interface maintenance       | 部分为维护信息     |
| 9999 | IP not authorized           | IP 未授权          |

### 通用响应字段

| 字段    | 类型    | 描述                                    |
|---------|---------|-----------------------------------------|
| Code    | Integer | 返回信息码，0 表示成功，其他均为失败    |
| Message | String  | 返回说明信息                            |
| Data    | Array   | 仅当 Code 等于 0 时返回                 |

---

## API 接口说明

### 1. 会员注册

- **方法**：`POST`
- **路径**：`/ley/register`

#### 请求参数

| 字段      | 类型   | 描述           |
|-----------|--------|----------------|
| account   | String | 商户账号       |
| api_key   | String | 商户密钥       |
| api_code  | String | 接口标识：AG、BBIN |
| username  | String | 注册会员账号   |
| password  | String | 注册会员密码   |

---

### 2. 会员登录游戏

- **方法**：`POST`
- **路径**：`/ley/login`（完整示例：`http://api.xxx.com/ley/login`）

#### 请求参数

| 字段      | 类型    | 描述                                      |
|-----------|---------|-------------------------------------------|
| account   | String  | 商户账号                                  |
| api_key   | String  | 商户密钥                                  |
| api_code  | String  | 接口标识：AG、BBIN                         |
| username  | String  | 注册时使用的会员账号                       |
| gameType  | String  | 游戏类型（参考通用规则）                   |
| gameCode  | String  | 子游戏代码（参考通用规则）                 |
| isMobile  | Integer | 0 = 电脑版，1 = 手机版，默认 0             |

#### 响应示例

**成功：**

```json
{
  "Code": 0,
  "Message": "成功",
  "Data": {
    "url": "https://www.baidu.com"
  }
}
```

**失败：** Code 非 0 时，根据 Message 和 Code 查看具体错误原因。
