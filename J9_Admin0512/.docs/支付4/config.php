<?php
error_reporting(E_ALL & ~E_NOTICE);
header('Content-type: text/html; charset=utf-8'); 
//平台分配的商户信息和密钥
define('merchantid','80008020');
define('merchantkey','e716b09724c64b0ab682e643d9f69dc3');

//请求网关，如果域名被风控，请商户自己解析一个子域名别名(CNAME)解析到www.jhyigou.com，之后网关域名改成自己的域名
define('gateway','http://www.koipayment.net:8896/api/order/create');
?>