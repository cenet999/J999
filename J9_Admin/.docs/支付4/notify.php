<?php
 include('config.php');
$data=$_GET;
$sign=$data['sign'];
unset($data['sign']);
ksort($data);
$unsign=urldecode(http_build_query($data)). merchantkey;

if($data['opstate']=='0'&&md5($unsign)==$sign){    //支付成功并且验签通过
        //入账操作，注意去重
        //可以根据$data['attach']和$data['orderid'] 来关联业务数据
         


      
        exit('success');       //成功处理完业务逻辑回写success不再接收通知
    }
?>