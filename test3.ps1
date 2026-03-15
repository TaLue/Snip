$base = "http://localhost:5123"
$pass = 0; $fail = 0

function Check($label, $cond) {
    if ($cond) { Write-Host "  [PASS] $label" -ForegroundColor Green; $script:pass++ }
    else        { Write-Host "  [FAIL] $label" -ForegroundColor Red;   $script:fail++ }
}
function FinalUrl($resp) {
    # PS 5.x: use ResponseUri  |  PS 7.x: use RequestMessage.RequestUri
    if ($resp.BaseResponse.ResponseUri) { return $resp.BaseResponse.ResponseUri.ToString() }
    if ($resp.BaseResponse.RequestMessage) { return $resp.BaseResponse.RequestMessage.RequestUri.ToString() }
    return ""
}
function Post($url, $body, $sess) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    return Invoke-WebRequest $url -Method POST -Body $bytes -ContentType "application/x-www-form-urlencoded" -UseBasicParsing -WebSession $sess -MaximumRedirection 10
}

$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# ─── 1. Homepage ──────────────────────────────────────────────
Write-Host "`n[1] Homepage"
$h = Invoke-WebRequest "$base/" -UseBasicParsing -WebSession $s
Check "Status 200"             ($h.StatusCode -eq 200)
Check "Has form"               ($h.Content -match 'action="/Home/Create"')
Check "Has originalUrl input"  ($h.Content -match 'name="originalUrl"')
Check "Has Tailwind CDN"       ($h.Content -match 'cdn.tailwindcss.com')
Check "No dashboard nav"       ($h.Content -notmatch 'href="/dashboard"')

# ─── 2. Create – custom slug ──────────────────────────────────
Write-Host "`n[2] Create custom slug"
$c = Post "$base/Home/Create" "originalUrl=https%3A%2F%2Fgithub.com%2FTaLue&customSlug=talue&label=GitHub" $s
Check "Returns 200"            ($c.StatusCode -eq 200)
# PS5.x does not capture cookies from 302 responses so TempData won't appear;
# verify creation worked by confirming /talue now redirects successfully
$verify = Invoke-WebRequest "$base/talue" -UseBasicParsing -WebSession $s -MaximumRedirection 10 -ErrorAction SilentlyContinue
Check "Link /talue was created" ($verify -and (FinalUrl $verify) -match 'github.com')
Check "Has form on page"        ($c.Content -match 'name="originalUrl"')

# ─── 3. Create – random slug ──────────────────────────────────
Write-Host "`n[3] Create random slug"
$c2 = Post "$base/Home/Create" "originalUrl=https%3A%2F%2Fexample.com%2Flong-path" $s
$slugHtml = [regex]::Match($c2.Content, 'localhost:5123/([a-z0-9]+)')
$slug = $slugHtml.Groups[1].Value
Check "Shows short URL"        ($c2.Content -match 'localhost:5123/')
Check "Slug is 6 chars"        ($slug.Length -eq 6)
Write-Host "    Random slug: $slug"

# ─── 4. Duplicate slug ────────────────────────────────────────
Write-Host "`n[4] Duplicate slug"
$c3 = Post "$base/Home/Create" "originalUrl=https%3A%2F%2Fexample.com&customSlug=talue" $s
Check "Shows error"            ($c3.Content -match 'Slug already taken')

# ─── 5. Empty URL ─────────────────────────────────────────────
Write-Host "`n[5] Empty URL"
$c4 = Post "$base/Home/Create" "originalUrl=" $s
Check "Shows required error"   ($c4.Content -match 'required')

# ─── 6. Redirect ──────────────────────────────────────────────
Write-Host "`n[6] Redirect (talue -> github.com)"
$redir = Invoke-WebRequest "$base/talue" -UseBasicParsing -WebSession $s -MaximumRedirection 10 -ErrorAction SilentlyContinue
$finalUrl = FinalUrl $redir
Check "Final URL is github.com" ($finalUrl -match 'github.com')
Write-Host "    Final URL: $finalUrl"

# ─── 7. Redirect logs click ───────────────────────────────────
Write-Host "`n[7] Click logging"
Invoke-WebRequest "$base/talue" -UseBasicParsing -WebSession $s -MaximumRedirection 10 -ErrorAction SilentlyContinue | Out-Null
Invoke-WebRequest "$base/talue" -UseBasicParsing -WebSession $s -MaximumRedirection 10 -ErrorAction SilentlyContinue | Out-Null
# (will verify click count = 3 after login)
Write-Host "    Clicked /talue 3 times total"

# ─── 8. Non-existent slug ─────────────────────────────────────
Write-Host "`n[8] Non-existent slug"
$nf = $null
try { $nf = Invoke-WebRequest "$base/zzz999xyz" -UseBasicParsing -ErrorAction Stop }
catch { $nf = $_.Exception.Response }
$nfCode = if ($nf.PSObject.Properties['StatusCode']) { [int]$nf.StatusCode } else { $nf.StatusCode.value__ }
Check "Returns 404"            ($nfCode -eq 404)

# ─── 9. Login page ────────────────────────────────────────────
Write-Host "`n[9] Login page"
$lp = Invoke-WebRequest "$base/login" -UseBasicParsing -WebSession $s
Check "Loads ok"               ($lp.StatusCode -eq 200)
Check "Has password input"     ($lp.Content -match 'type="password"')

# ─── 10. Wrong password ───────────────────────────────────────
Write-Host "`n[10] Wrong password"
$wp = Post "$base/login" "password=wrongpass" $s
Check "Shows error"            ($wp.Content -match 'Wrong password')
Check "Not redirected"         ($wp.Content -match 'type="password"')

# ─── 11. Dashboard blocked without auth ───────────────────────
Write-Host "`n[11] Dashboard without auth"
$db0 = Invoke-WebRequest "$base/dashboard" -UseBasicParsing -WebSession $s -MaximumRedirection 10
$db0Url = FinalUrl $db0
Check "Redirected to login"    ($db0Url -match 'login')
Write-Host "    Final URL: $db0Url"

# ─── 12. Login correct ────────────────────────────────────────
Write-Host "`n[12] Login"
$li = Post "$base/login" "password=changeme" $s
$liUrl = FinalUrl $li
Check "Redirected to dashboard" ($liUrl -match 'dashboard')
Check "Shows /talue slug"       ($li.Content -match '/talue')
Check "Shows click count > 0"   ($li.Content -match '>[1-9][0-9]*<')
Check "Nav shows Dashboard"     ($li.Content -match 'href="/dashboard"')
Write-Host "    Final URL: $liUrl"

# ─── 13. Dashboard detail ─────────────────────────────────────
Write-Host "`n[13] Dashboard detail"
# In the card HTML: slug appears first, then Stats/Toggle links
# So pattern is: /talue -> ... -> /dashboard/{id}
$html = $li.Content
$talueId = ([regex]::Match($html, '(?s)/talue[^/].*?/dashboard/(\d+)')).Groups[1].Value
if (-not $talueId) {
    $talueId = ([regex]::Match($html, '(?s)/talue.*?action="/dashboard/(\d+)/toggle"')).Groups[1].Value
}
if (-not $talueId) {
    $talueId = ([regex]::Match($html, 'href="/dashboard/(\d+)"')).Groups[1].Value
}
Write-Host "    Talue link ID: '$talueId'"
if ($talueId) {
    $dd = Invoke-WebRequest "$base/dashboard/$talueId" -UseBasicParsing -WebSession $s
    Check "Detail loads"           ($dd.StatusCode -eq 200)
    Check "Shows /talue"           ($dd.Content -match '/talue')
    Check "Shows total clicks"     ($dd.Content -match 'total clicks')
    Check "Shows referrers"        ($dd.Content -match 'Top referrers')
    Check "Has Chart.js (has data)"($dd.Content -match 'chart.js')
    $clickCount = [regex]::Match($dd.Content, '(\d+)</div>\s*<div[^>]*>total clicks').Groups[1].Value
    Write-Host "    Clicks recorded: $clickCount"
} else {
    Check "Found talue link ID" $false
}

# ─── 14. Toggle ───────────────────────────────────────────────
Write-Host "`n[14] Toggle inactive"
if ($talueId) {
    $tg = Post "$base/dashboard/$talueId/toggle" "" $s
    $tgUrl = FinalUrl $tg
    Check "Redirects to dashboard" ($tgUrl -match 'dashboard')
    Check "Shows inactive badge"   ($tg.Content -match 'inactive')

    $dis = $null
    try { $dis = Invoke-WebRequest "$base/talue" -UseBasicParsing -WebSession $s -ErrorAction Stop }
    catch { $dis = $_.Exception.Response }
    $disCode = if ($dis.PSObject.Properties['StatusCode']) { [int]$dis.StatusCode } else { $dis.StatusCode.value__ }
    Check "Inactive slug = 404"    ($disCode -eq 404)

    Post "$base/dashboard/$talueId/toggle" "" $s | Out-Null
    Write-Host "    Re-enabled"
}

# ─── 15. Logout ───────────────────────────────────────────────
Write-Host "`n[15] Logout"
$lo = Post "$base/logout" "" $s
$loUrl = FinalUrl $lo
Check "Redirected to home"     ($loUrl -match "5123/$")
Write-Host "    Final URL: $loUrl"

$db2 = Invoke-WebRequest "$base/dashboard" -UseBasicParsing -WebSession $s -MaximumRedirection 10
$db2Url = FinalUrl $db2
Check "Dashboard blocked"      ($db2Url -match 'login')
Write-Host "    Final URL: $db2Url"

# ─── Summary ──────────────────────────────────────────────────
$total = $pass + $fail
Write-Host "`n────────────────────────────────────"
Write-Host "  PASSED: $pass / $total" -ForegroundColor Green
if ($fail -gt 0) { Write-Host "  FAILED: $fail / $total" -ForegroundColor Red }
Write-Host "────────────────────────────────────"
