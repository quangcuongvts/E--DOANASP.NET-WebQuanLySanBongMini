Add-Type -AssemblyName System.Net.Http
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5196'
function New-Client {
    $h = [System.Net.Http.HttpClientHandler]::new(); $h.AllowAutoRedirect = $false; $h.UseCookies = $true; $h.CookieContainer = [System.Net.CookieContainer]::new(); return [System.Net.Http.HttpClient]::new($h)
}
function Get-Html($c, $p) { $r = $c.GetAsync("$base$p").GetAwaiter().GetResult(); return @{ Status=[int]$r.StatusCode; Body=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult(); Location=$r.Headers.Location } }
function Token($html) { return [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value }
function Post-Form($c, $p, $fields) {
    $pairs = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    foreach ($k in $fields.Keys) { $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new($k, [string]$fields[$k])) }
    $r = $c.PostAsync("$base$p", [System.Net.Http.FormUrlEncodedContent]::new($pairs)).GetAwaiter().GetResult()
    return @{ Status=[int]$r.StatusCode; Body=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult(); Location=$r.Headers.Location }
}
function Login($e,$p) { $c = New-Client; $page = Get-Html $c '/Account/Login'; Post-Form $c '/Account/Login' @{ Email=$e; Password=$p; RememberMe='false'; __RequestVerificationToken=(Token $page.Body) } | Out-Null; return $c }

$customer = Login 'customer@football.com' 'Customer@123'
$detail = Get-Html $customer '/Booking/Details/78'
$cancel = Post-Form $customer '/Booking/Cancel' @{ Id='78'; Reason='Doi lich dot xuat'; __RequestVerificationToken=(Token $detail.Body) }
$after = Get-Html $customer '/Booking/Details/78'
Write-Output "cancel $($cancel.Status) has-cancel=$($after.Body -match 'BK202609220014')"

$slots = (Get-Html $customer '/Booking/Slots?fieldId=2&date=2026-09-27').Body | ConvertFrom-Json
$free = @($slots.slots | Where-Object status -eq 'available' | Sort-Object start)
$start=$free[0].start; $end=$free[0].end
$created = Post-Form $customer '/Booking/Create' @{ FieldId='2'; BookingDate='2026-09-27'; StartTime=$start; EndTime=$end; PaymentMethod='2'; __RequestVerificationToken=(Token (Get-Html $customer '/Booking/Create').Body) }
Write-Output "qr-create $($created.Status) $($created.Location)"
if ($created.Location) {
    $id = [regex]::Match($created.Location.PathAndQuery, '(\d+)$').Groups[1].Value
    $page = Get-Html $customer $created.Location.PathAndQuery
    $pay = Post-Form $customer "/Booking/Pay/$id" @{ id=$id; method='QRCode'; success='true'; __RequestVerificationToken=(Token $page.Body) }
    $done = Get-Html $customer "/Booking/Details/$id"
    Write-Output "pay $($pay.Status) confirmed-text=$($done.Body -match 'status st-confirmed|st-paid')"
    $inv = Get-Html $customer "/Booking/Invoice/$id"
    Write-Output "invoice $($inv.Status)"
}

$review = Post-Form $customer '/Reviews/Create' @{ BookingId='19'; Rating='5'; Comment='San dep, co moi, den rat sang.'; __RequestVerificationToken=(Token (Get-Html $customer '/Reviews/Create?bookingId=19').Body) }
Write-Output "review $($review.Status) $($review.Location)"

$manager = Login 'manager@football.com' 'Manager@123'
$del = Post-Form $manager '/Admin/Fields/Delete' @{ id='11'; __RequestVerificationToken=(Token (Get-Html $manager '/Admin/Fields').Body) }
Write-Output "delete-field $($del.Status) $($del.Location)"

$contactClient = New-Client
$contact = Post-Form $contactClient '/Home/Contact' @{ FullName='Le Minh'; Email='le.minh@gmail.com'; Phone='0901234567'; Subject='Hoi san trong'; Message='Cho minh hoi san 5 nguoi toi thu Bay.'; __RequestVerificationToken=(Token (Get-Html $contactClient '/Home/Contact').Body) }
Write-Output "contact $($contact.Status) $($contact.Location)"
