Add-Type -AssemblyName System.Net.Http
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5196'
$results = @()

function New-Client {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $handler.UseCookies = $true
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    return $client
}

function Get-Html([System.Net.Http.HttpClient]$client, [string]$path) {
    $response = $client.GetAsync("$base$path").GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return @{ Status = [int]$response.StatusCode; Body = $body; Location = $response.Headers.Location }
}

function Get-Token([string]$html) {
    $m = [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"')
    if (-not $m.Success) { $m = [regex]::Match($html, 'name="__RequestVerificationToken" value="([^"]+)"') }
    if (-not $m.Success) { throw 'Missing antiforgery token' }
    return $m.Groups[1].Value
}

function Post-Form([System.Net.Http.HttpClient]$client, [string]$path, [hashtable]$fields) {
    $pairs = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    foreach ($key in $fields.Keys) {
        $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new($key, [string]$fields[$key]))
    }
    $content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    $response = $client.PostAsync("$base$path", $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return @{ Status = [int]$response.StatusCode; Body = $body; Location = $response.Headers.Location }
}

function Login([string]$email, [string]$password) {
    $client = New-Client
    $page = Get-Html $client '/Account/Login'
    $token = Get-Token $page.Body
    $posted = Post-Form $client '/Account/Login' @{
        Email = $email
        Password = $password
        RememberMe = 'false'
        __RequestVerificationToken = $token
    }
    return @{ Client = $client; Status = $posted.Status; Location = [string]$posted.Location; Body = $posted.Body }
}

function Note($name, $ok, $detail) {
    $script:results += ('{0} {1} {2}' -f ($(if ($ok) {'OK'} else {'FAIL'})), $name, $detail)
}

$guestCreate = Get-Html (New-Client) '/Booking/Create'
Note 'guest-book-redirect' ($guestCreate.Status -eq 302 -and $guestCreate.Location.AbsolutePath -eq '/Account/Login') $guestCreate.Location

$customer = Login 'customer@football.com' 'Customer@123'
Note 'customer-login' ($customer.Status -eq 302) "$($customer.Status) $($customer.Location)"
$dash = Get-Html $customer.Client '/Account/Dashboard'
Note 'customer-dashboard' ($dash.Status -eq 200 -and $dash.Body -match 'Xin ch') $dash.Status

$adminAsCustomer = Get-Html $customer.Client '/Admin'
Note 'customer-blocked-admin' ($adminAsCustomer.Status -eq 302 -and ([string]$adminAsCustomer.Location) -match 'AccessDenied') ([string]$adminAsCustomer.Location)

$date = (Get-Date).AddDays(3).ToString('yyyy-MM-dd')
$slots = Get-Html $customer.Client "/Booking/Slots?fieldId=1&date=$date"
$slotJson = $slots.Body | ConvertFrom-Json
$available = @($slotJson.slots | Where-Object { $_.status -eq 'available' } | Sort-Object start)
$start = $null
$end = $null
for ($i = 0; $i -lt $available.Count - 1; $i++) {
    if ($available[$i].end -eq $available[$i + 1].start) {
        $start = $available[$i].start
        $end = $available[$i + 1].end
        break
    }
}
Note 'slots' ($slots.Status -eq 200 -and $start) "available=$($available.Count) range=$start-$end"
$page = Get-Html $customer.Client '/Booking/Create'
$token = Get-Token $page.Body
$quoteBody = @{
    fieldId = 1
    date = $date
    start = $start
    end = $end
    voucherCode = 'FOOTBALL10'
    services = @(@{ serviceId = 1; quantity = 1 })
} | ConvertTo-Json
$quoteRequest = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "$base/Booking/Quote")
$quoteRequest.Content = [System.Net.Http.StringContent]::new($quoteBody, [System.Text.Encoding]::UTF8, 'application/json')
$quoteRequest.Headers.Add('RequestVerificationToken', $token)
$quoteResponse = $customer.Client.SendAsync($quoteRequest).GetAwaiter().GetResult()
$quoteText = $quoteResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
$quote = $quoteText | ConvertFrom-Json
Note 'quote-voucher' ($quote.ok -eq $true -and $quote.discount -gt 0) "field=$($quote.fieldAmount) discount=$($quote.discount) total=$($quote.total)"

$peakBody = @{ fieldId = 1; date = $date; start = '18:00'; end = '19:00'; services = @() } | ConvertTo-Json
$dayBody = @{ fieldId = 1; date = $date; start = '10:00'; end = '11:00'; services = @() } | ConvertTo-Json
function Quote-Raw($json) {
    $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "$base/Booking/Quote")
    $req.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, 'application/json')
    $req.Headers.Add('RequestVerificationToken', $token)
    $res = $customer.Client.SendAsync($req).GetAwaiter().GetResult()
    return ($res.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
}
$peak = Quote-Raw $peakBody
$day = Quote-Raw $dayBody
Note 'peak-price' ($peak.ok -and $day.ok -and $peak.fieldAmount -gt $day.fieldAmount) "day=$($day.fieldAmount) peak=$($peak.fieldAmount)"

$create = Post-Form $customer.Client '/Booking/Create' @{
    FieldId = '1'
    BookingDate = $date
    StartTime = $start
    EndTime = $end
    Note = 'Test dat san'
    VoucherCode = 'FOOTBALL10'
    PaymentMethod = '2'
    'Services[0].ServiceId' = '1'
    'Services[0].Quantity' = '1'
    __RequestVerificationToken = $token
}
Note 'create-booking' ($create.Status -eq 302 -and ([string]$create.Location) -match '/Booking/') "$($create.Status) $($create.Location) $($create.Body.Substring(0, [Math]::Min(180, $create.Body.Length)))"

$dup = Post-Form $customer.Client '/Booking/Create' @{
    FieldId = '1'
    BookingDate = $date
    StartTime = $start
    EndTime = $end
    PaymentMethod = '0'
    __RequestVerificationToken = (Get-Token (Get-Html $customer.Client '/Booking/Create').Body)
}
Note 'reject-overlap' ($dup.Status -eq 200 -and $dup.Body -match 'giữ|trùng|bận|không') $dup.Status

if ($create.Location) {
    $detailPath = $create.Location.PathAndQuery
    $detail = Get-Html $customer.Client $detailPath
    Note 'booking-detail' ($detail.Status -eq 200 -and $detail.Body -match 'BK') $detail.Status
    if ($detail.Body -match 'href="(/Booking/Payment/\d+)"') {
        $payPage = Get-Html $customer.Client $Matches[1]
        $payToken = Get-Token $payPage.Body
        $pay = Post-Form $customer.Client ($Matches[1].Replace('/Payment/','/Pay/')) @{
            method = '2'
            success = 'true'
            __RequestVerificationToken = $payToken
        }
        Note 'payment' ($pay.Status -eq 302) "$($pay.Status) $($pay.Location)"
    } else {
        Note 'payment' $false 'no payment link'
    }
}

$mine = Get-Html $customer.Client '/Booking/Mine'
Note 'history' ($mine.Status -eq 200 -and $mine.Body -match 'BK') $mine.Status

$fields = Get-Html (New-Client) '/Fields?q=Champions&sort=price-asc&typeId=2'
Note 'search-fields' ($fields.Status -eq 200) $fields.Status

$regPage = Get-Html (New-Client) '/Account/Register'
$regClient = New-Client
$regHtml = Get-Html $regClient '/Account/Register'
$stamp = Get-Date -Format 'HHmmss'
$reg = Post-Form $regClient '/Account/Register' @{
    FullName = 'Khach Test'
    Email = "test$stamp@football.com"
    Phone = "09$stamp" + "01"
    Password = 'Test@123'
    ConfirmPassword = 'Test@123'
    __RequestVerificationToken = (Get-Token $regHtml.Body)
}
Note 'register' ($reg.Status -eq 302) "$($reg.Status) $($reg.Location) $($reg.Body.Substring(0,[Math]::Min(160,$reg.Body.Length)))"

$admin = Login 'admin@football.com' 'Admin@123'
Note 'admin-login' ($admin.Status -eq 302) "$($admin.Status) $($admin.Location)"
$adminHome = Get-Html $admin.Client '/Admin'
Note 'admin-dashboard' ($adminHome.Status -eq 200 -and $adminHome.Body -match 'chart7|Tổng doanh thu|Doanh thu') $adminHome.Status
$users = Get-Html $admin.Client '/Admin/Users?q=admin&role=Admin&status=active'
Note 'admin-users' ($users.Status -eq 200 -and $users.Body -match 'admin@football.com') $users.Status
$report = Get-Html $admin.Client '/Admin/Reports/Revenue'
Note 'admin-report' ($report.Status -eq 200 -and $report.Body -match 'reportChart') $report.Status

$staff = Login 'staff@football.com' 'Staff@123'
Note 'staff-login' ($staff.Status -eq 302) "$($staff.Status) $($staff.Location)"
$staffHome = Get-Html $staff.Client '/Admin'
Note 'staff-dashboard' ($staffHome.Status -eq 200 -and $staffHome.Body -match 'Booking hôm nay|Lịch sân hôm nay') $staffHome.Status
$staffUsers = Get-Html $staff.Client '/Admin/Users'
Note 'staff-blocked-users' ($staffUsers.Status -eq 302 -and ([string]$staffUsers.Location) -match 'AccessDenied') ([string]$staffUsers.Location)

$manager = Login 'manager@football.com' 'Manager@123'
$prices = Get-Html $manager.Client '/Admin/PriceRules'
Note 'manager-prices' ($prices.Status -eq 200 -and $prices.Body -match 'Bảng giá') $prices.Status
$fieldForm = Get-Html $manager.Client '/Admin/Fields/Create'
Note 'manager-field-form' ($fieldForm.Status -eq 200 -and $fieldForm.Body -match 'Mã sân') $fieldForm.Status

$results | ForEach-Object { $_ }
