 <?php
include('config.php');

//通知回调地址，在这里进行效验和入账操作，地址中不要包含"?"参数
$callbackurl = 'http://' . $_SERVER['HTTP_HOST'] . '/notify.php';  
//支付成功后跳转地址,请不要包含"?"参数
$hrefbackurl = 'http://' . $_SERVER['HTTP_HOST'] . $_SERVER['REQUEST_URI'];

?>

<form id="payfrom" action="" method="post" target="_blank">
        <div>
            商&nbsp;&nbsp;户&nbsp;&nbsp;号:<input type="text" name="merchantid" value="<?php echo merchantid ?>"/><p />
            商户密钥:<input type="text" name="merchantkey" value="<?php echo merchantkey ?>" /><p />
            金&nbsp;&nbsp;&nbsp;&nbsp;额:<input type="text" name="amount" value="100" />  *部分通道可能有金额规则请咨询客服<p />
           通道ID:
            <?php foreach (array(
              '1004' => '微信扫码',
              '1007' => '微信wap',
              '992' => '支付宝扫码',
              '1006' => '支付宝wap',
              '1000' => '银联扫码',
              '1005' => '银联快捷',
               '1002' => '网银'
            ) as $k => $v) {
              printf("<label><input type=\"radio\" name=\"type\" value=\"%s\" %s />%s</label>", $k, $k == '992' ? 'checked' : '', $v);
            } ?>
            <!--<p /><label><input type="checkbox" name="returnjson" value="true" />扫码返回json</label>-->
            <p /><p />
            <input type="submit" value="开始支付" />
          </div>
    </form>
<?php

if ($_SERVER['REQUEST_METHOD'] == "POST") {
    //商户id
  $p['merchantid'] = $_POST['merchantid'];
    //通道ID
  $p['type'] = $_POST['type'];    //微信扫码1004,微信wap1007,支付宝扫码992,支付宝wap1006,银联扫码1000,银联快捷1005
    //交易金额，部分通道有最低金额限制
  $p['value'] = $_POST['amount'];
    //订单号不能重复出现
  $p['orderid'] = uniqid(null, true);
    //通知回调地址，在这里进行效验和入账操作，地址中不要包含"?"参数
  $p['callbackurl'] = $callbackurl;     
    //签名

  $p['sign'] = md5(urldecode(http_build_query($p)) . $_POST['merchantkey']);
    //用户自定义数据(如用户名/id)，回调时会原样传回，此参数可以省略
  $p['attach'] = 'userid';     
    //支付成功后跳转地址,请不要包含"?"参数
  $p['hrefbackurl'] = $hrefbackurl;  
  
  
  //get版本生成支付url
  /*$payurl=gateway.'?'.http_build_query($p);
  header("Location: $payurl");
  exit();
   */

  if (isset($_POST['returnjson']) && in_array($p['type'], array('1004', '992', '1000'))) {

    $p['respType']='json';
    $curl = curl_init();
    curl_setopt($curl, CURLOPT_URL, $gw);
    curl_setopt($curl, CURLOPT_POST, 1);
    curl_setopt($curl, CURLOPT_POSTFIELDS, $p);

    curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
    curl_setopt($curl, CURLOPT_TIMEOUT, 10);

    $data = curl_exec($curl);
    if (curl_errno($curl)) {
    //return curl_error($curl);
    }
    curl_close($curl);
    echo $data;

  } else {
    echo "<form action=\"".gateway."\" method=\"post\">";
    foreach ($p as $k => $v) {
      echo "<input type=\"hidden\" name=\"$k\" value=\"$v\"/>";
    }
    echo '</form>',
      '<script type="text/javascript">document.forms[1].submit()</script>';
  }
}
?>