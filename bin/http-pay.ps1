Add-Type -AssemblyName System.Net.Http
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5196'
$h = [System.Net.Http.HttpClientHandler]::new(); $h.AllowAutoRedirect = $false; $h.UseCookies = $true; $h.CookieContainer = [System.Net.CookieContainer]::new()
$c = [System.Net.Http.HttpClient]::new($h)
function Get-Html($p) { $r = $c.GetAsync("$base$p").GetAwaiter().GetResult(); return @{ Status=[int]$r.StatusCode; Body=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult() } }
function Token($html) { return [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value }
function Post-Form($p, $fields) {
    $pairs = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    foreach ($k in $fields.Keys) { $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new($k, [string]$fields[$k])) }
    $r = $c.PostAsync("$base$p", [System.Net.Http.FormUrlEncodedContent]::new($pairs)).GetAwaiter().GetResult()
    $body = $r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    Write-Output "$p -> $([int]$r.StatusCode) $($r.Headers.Location) body=$($body.Substring(0, [Math]::Min(120, $body.Length)))"
}
$login = Get-Html '/Account/Login'
Post-Form '/Account/Login' @{ Email='customer@football.com'; Password='Customer@123'; RememberMe='false'; __RequestVerificationToken=(Token $login.Body) } | Out-Null
$page = Get-Html '/Booking/Payment/79'
Write-Output "payment-page $($page.Status) token=$($page.Body.Contains('__RequestVerificationToken'))"
Post-Form '/Booking/Pay' @{ id='79'; method='QRCode'; success='true'; __RequestVerificationToken=(Token $page.Body) }
$detail = Get-Html '/Booking/Details/79'
Write-Output "detail $($detail.Status) paid=$($detail.Body.Contains('st-paid'))"
$inv = Get-Html '/Booking/Invoice/79'
Write-Output "invoice $($inv.Status)"
