Add-Type -AssemblyName System.Net.Http
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5196'
$results = @()

function New-Client {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $handler.UseCookies = $true
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    return [System.Net.Http.HttpClient]::new($handler)
}
function Get-Html($client, $path) {
    $response = $client.GetAsync("$base$path").GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return @{ Status = [int]$response.StatusCode; Body = $body; Location = $response.Headers.Location }
}
function Get-Token($html) {
    $m = [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"')
    if (-not $m.Success) { throw 'no token' }
    return $m.Groups[1].Value
}
function Post-Form($client, $path, $fields) {
    $pairs = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    foreach ($key in $fields.Keys) { $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new($key, [string]$fields[$key])) }
    $content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    $response = $client.PostAsync("$base$path", $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return @{ Status = [int]$response.StatusCode; Body = $body; Location = $response.Headers.Location }
}
function Login($email, $password) {
    $client = New-Client
    $page = Get-Html $client '/Account/Login'
    $posted = Post-Form $client '/Account/Login' @{ Email = $email; Password = $password; RememberMe = 'false'; __RequestVerificationToken = (Get-Token $page.Body) }
    return $client
}
function Note($name, $ok, $detail) { $script:results += ('{0} {1} {2}' -f ($(if ($ok) {'OK'} else {'FAIL'})), $name, $detail) }
function Temp($html) {
    $m = [regex]::Match($html, 'window\.tempError = (".*?");')
    if ($m.Success) { return $m.Groups[1].Value }
    $m = [regex]::Match($html, 'class="validation"[^>]*>([^<]+)')
    if ($m.Success) { return $m.Groups[1].Value.Trim() }
    return ''
}
function Quote($client, $token, $payload) {
    $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "$base/Booking/Quote")
    $req.Content = [System.Net.Http.StringContent]::new(($payload | ConvertTo-Json), [Text.Encoding]::UTF8, 'application/json')
    $req.Headers.Add('RequestVerificationToken', $token)
    $res = $client.SendAsync($req).GetAwaiter().GetResult()
    return ($res.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
}

$customer = Login 'customer@football.com' 'Customer@123'
$date = (Get-Date).AddDays(4).ToString('yyyy-MM-dd')
$slots = (Get-Html $customer "/Booking/Slots?fieldId=1&date=$date").Body | ConvertFrom-Json
$free = @($slots.slots | Where-Object status -eq 'available' | Sort-Object start)
$ranges = @()
for ($i = 0; $i -lt $free.Count - 1; $i++) {
    if ($free[$i].end -eq $free[$i+1].start) { $ranges += ,@($free[$i].start, $free[$i+1].end) }
}
$token = Get-Token (Get-Html $customer '/Booking/Create').Body
$quote = Quote $customer $token @{ fieldId = 1; date = $date; start = $ranges[0][0]; end = $ranges[0][1]; voucherCode = 'WEEKEND100'; services = @(@{ serviceId = 1; quantity = 1 }) }
Note 'weekend-voucher' ($quote.ok -and [decimal]$quote.discount -eq 100000) "ok=$($quote.ok) discount=$($quote.discount) msg=$($quote.message) total=$($quote.total)"

$fresh = New-Client
$stamp = Get-Random -Minimum 10000000 -Maximum 99999999
$reg = Post-Form $fresh '/Account/Register' @{
    FullName = 'Khach Voucher'
    Email = "voucher$stamp@football.com"
    Phone = "09$stamp"
    Password = 'Test@123'
    ConfirmPassword = 'Test@123'
    __RequestVerificationToken = (Get-Token (Get-Html $fresh '/Account/Register').Body)
}
Note 'register2' ($reg.Status -eq 302) "$($reg.Status)"
$token2 = Get-Token (Get-Html $fresh '/Booking/Create').Body
$quote2 = Quote $fresh $token2 @{ fieldId = 1; date = $date; start = '10:00'; end = '12:00'; voucherCode = 'FOOTBALL10'; services = @() }
$expected = [Math]::Min(100000, [Math]::Round([decimal]$quote2.fieldAmount * 0.10, 0))
Note 'football10-math' ($quote2.ok -and [decimal]$quote2.discount -eq $expected) "field=$($quote2.fieldAmount) discount=$($quote2.discount) expected=$expected msg=$($quote2.message)"

$create = Post-Form $customer '/Booking/Create' @{
    FieldId = '1'; BookingDate = $date; StartTime = $ranges[0][0]; EndTime = $ranges[0][1]
    Note = 'Kiem thu dat san'; VoucherCode = 'WEEKEND100'; PaymentMethod = '0'
    'Services[0].ServiceId' = '1'; 'Services[0].Quantity' = '1'
    __RequestVerificationToken = (Get-Token (Get-Html $customer '/Booking/Create').Body)
}
Note 'create-cash' ($create.Status -eq 302 -and ([string]$create.Location) -match '/Booking/Details/') "$($create.Status) $($create.Location) $(Temp $create.Body)"

$dup = Post-Form $customer '/Booking/Create' @{
    FieldId = '1'; BookingDate = $date; StartTime = $ranges[0][0]; EndTime = $ranges[0][1]; PaymentMethod = '0'
    __RequestVerificationToken = (Get-Token (Get-Html $customer '/Booking/Create').Body)
}
Note 'overlap' ($dup.Status -eq 200 -and (Temp $dup.Body) -match 'giữ|trùng|bận') (Temp $dup.Body)

if ($create.Location) {
    $path = $create.Location.PathAndQuery
    $detail = Get-Html $customer $path
    $cancel = Post-Form $customer '/Booking/Cancel' @{
        Id = ([regex]::Match($path, '\d+').Value)
        Reason = 'Doi lich dot xuat'
        __RequestVerificationToken = (Get-Token $detail.Body)
    }
    $after = Get-Html $customer $path
    Note 'cancel' ($cancel.Status -eq 302 -and $after.Body -match 'Đã hủy|hủy|Hoàn') "$($cancel.Status) $(Temp $after.Body)"
}

$payCreate = Post-Form $customer '/Booking/Create' @{
    FieldId = '1'; BookingDate = $date; StartTime = $ranges[1][0]; EndTime = $ranges[1][1]; PaymentMethod = '2'
    __RequestVerificationToken = (Get-Token (Get-Html $customer '/Booking/Create').Body)
}
Note 'create-qr' ($payCreate.Status -eq 302 -and ([string]$payCreate.Location) -match 'Payment') "$($payCreate.Status) $($payCreate.Location) $(Temp $payCreate.Body)"
if ($payCreate.Location -and ([string]$payCreate.Location) -match 'Payment') {
    $payPath = $payCreate.Location.PathAndQuery
    $payPage = Get-Html $customer $payPath
    $id = [regex]::Match($payPath, '\d+').Value
    $paid = Post-Form $customer "/Booking/Pay/$id" @{
        id = $id; method = 'QRCode'; success = 'true'
        __RequestVerificationToken = (Get-Token $payPage.Body)
    }
    $paidDetail = Get-Html $customer "/Booking/Details/$id"
    Note 'pay' ($paid.Status -eq 302 -and $paidDetail.Body -match 'Đã thanh toán|Paid|Đã xác nhận') "$($paid.Status)"
}

$admin = Login 'admin@football.com' 'Admin@123'
$pages = @(
  '/Admin','/Admin/Fields','/Admin/Fields/Details/1','/Admin/FieldTypes','/Admin/Schedules','/Admin/Bookings','/Admin/Bookings?page=2',
  '/Admin/Customers','/Admin/Staffs','/Admin/Payments','/Admin/Services','/Admin/Vouchers','/Admin/PriceRules',
  '/Admin/Users','/Admin/Users?page=2','/Admin/Roles','/Admin/Reviews','/Admin/Maintenances','/Admin/Posts','/Admin/Banners',
  '/Admin/Contacts','/Admin/AuditLogs','/Admin/Settings','/Admin/Reports/Bookings','/Admin/Reports/Customers','/Admin/Reports/Performance'
)
$bad = @()
foreach ($p in $pages) {
    $r = Get-Html $admin $p
    if ($r.Status -ne 200) { $bad += "$($r.Status) $p" }
}
Note 'admin-pages' ($bad.Count -eq 0) ($(if ($bad.Count) { $bad -join '; ' } else { 'all 200' }))

$staff = Login 'staff@football.com' 'Staff@123'
$staffHome = Get-Html $staff '/Admin'
Note 'staff-home' ($staffHome.Status -eq 200 -and $staffHome.Body -match 'kpi-card') $staffHome.Status
$lookup = Get-Html $staff '/Admin/Bookings/Details/63'
$check = Post-Form $staff '/Admin/Bookings/ConfirmPayment' @{ id = '63'; method = 'Cash'; __RequestVerificationToken = (Get-Token $lookup.Body) }
$checkIn = Post-Form $staff '/Admin/Bookings/CheckIn' @{ Id = '63'; Note = 'Khach den dung gio'; __RequestVerificationToken = (Get-Token (Get-Html $staff '/Admin/Bookings/Details/63').Body) }
$start = Post-Form $staff '/Admin/Bookings/Start' @{ id = '63'; __RequestVerificationToken = (Get-Token (Get-Html $staff '/Admin/Bookings/Details/63').Body) }
$out = Post-Form $staff '/Admin/Bookings/CheckOut' @{ id = '63'; confirmPayment = 'true'; method = 'Cash'; __RequestVerificationToken = (Get-Token (Get-Html $staff '/Admin/Bookings/Details/63').Body) }
$final = Get-Html $staff '/Admin/Bookings/Details/63'
Note 'staff-flow' ($final.Body -match 'Hoàn thành') "pay=$(Temp $check.Body) in=$(Temp $checkIn.Body) start=$(Temp $start.Body) out=$(Temp $out.Body)"

$manager = Login 'manager@football.com' 'Manager@123'
$pricePage = Get-Html $manager '/Admin/PriceRules/Edit/1'
Note 'price-edit-page' ($pricePage.Status -eq 200 -and $pricePage.Body -match 'name="PricePerHour"') $pricePage.Status
$fieldPage = Get-Html $manager '/Admin/Fields/Create'
Note 'field-create-page' ($fieldPage.Status -eq 200 -and $fieldPage.Body -match 'name="FieldCode"') $fieldPage.Status
$code = 'FT' + (Get-Random -Minimum 100 -Maximum 999)
$createdField = Post-Form $manager '/Admin/Fields/Create' @{
    FieldCode = $code; Name = 'San kiem thu'; FieldTypeId = '1'; Description = 'San test'; Capacity = '10'
    Length = '25'; Width = '15'; SurfaceType = 'Co nhan tao'; BasePrice = '300000'; Address = 'Quan 1'; Area = 'Quan 1'
    Status = 'Available'; HasLighting = 'true'; HasChangingRoom = 'true'; HasToilet = 'true'; HasParking = 'true'; HasWater = 'true'; IsActive = 'true'
    __RequestVerificationToken = (Get-Token $fieldPage.Body)
}
Note 'field-create' ($createdField.Status -eq 302) "$($createdField.Status) $($createdField.Location) $(Temp $createdField.Body)"

$png = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==')
[IO.File]::WriteAllBytes("$pwd\bin\avatar-test.png", $png)
$profile = Get-Html $customer '/Account/Profile'
$mp = [System.Net.Http.MultipartFormDataContent]::new()
foreach ($pair in @(
    @('__RequestVerificationToken', (Get-Token $profile.Body)),
    @('FullName', ([regex]::Match($profile.Body, 'name="FullName"[^>]*value="([^"]*)"').Groups[1].Value)),
    @('Email', ([regex]::Match($profile.Body, 'name="Email"[^>]*value="([^"]*)"').Groups[1].Value)),
    @('Phone', ([regex]::Match($profile.Body, 'name="Phone"[^>]*value="([^"]*)"').Groups[1].Value)),
    @('Gender', 'Male'),
    @('Address', 'Quan 1')
)) { $mp.Add([System.Net.Http.StringContent]::new($pair[1]), $pair[0]) }
$file = [System.Net.Http.ByteArrayContent]::new($png)
$file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
$mp.Add($file, 'avatar', 'avatar.png')
$uploaded = $customer.PostAsync("$base/Account/Profile", $mp).GetAwaiter().GetResult()
$uploadBody = $uploaded.Content.ReadAsStringAsync().GetAwaiter().GetResult()
Note 'avatar-upload' ([int]$uploaded.StatusCode -eq 302) "$([int]$uploaded.StatusCode) $($uploaded.Headers.Location) $(Temp $uploadBody)"

$results | ForEach-Object { $_ }
