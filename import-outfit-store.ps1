$ErrorActionPreference = "Stop"

$storePath = ".\outfit_planner_back\src\OutfitPlanner.Api\storage\outfit-store.json"
$store = Get-Content $storePath -Raw | ConvertFrom-Json

$composeArgs = @(
  "--env-file", ".env",
  "-f", "docker-compose.dev.yml",
  "-f", "docker-compose.selfhost.override.yml"
)

# ВАЖНО:
# Если ты НЕ задавал ObjectStorage__Local__SigningSecret отдельно, оставь так.
# Это дефолт из LocalObjectStorage.
$signingSecret = "local-object-storage-development-signing-key"

function SqlText($value) {
    if ($null -eq $value) { return "NULL" }
    $s = [string]$value
    return "'" + ($s -replace "'", "''") + "'"
}

function SqlBool($value) {
    if ($null -eq $value) { return "false" }
    if ([bool]$value) { return "true" }
    return "false"
}

function SqlInt($value) {
    if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
        return "NULL"
    }
    return [string][int]$value
}

function SqlTextArray($value) {
    if ($null -eq $value) {
        return "ARRAY[]::text[]"
    }

    $items = @($value) | Where-Object { $null -ne $_ -and -not [string]::IsNullOrWhiteSpace([string]$_) }

    if ($items.Count -eq 0) {
        return "ARRAY[]::text[]"
    }

    return "ARRAY[" + (($items | ForEach-Object { SqlText $_ }) -join ",") + "]::text[]"
}

function Get-ObjectKeyFromSignedUrl($url) {
    if ([string]::IsNullOrWhiteSpace($url)) {
        return $null
    }

    $path = $url
    try {
        $uri = [Uri]$url
        $path = $uri.AbsolutePath
    } catch {
        $path = ($url -split "\?")[0]
    }

    $prefix = "/api/storage/signed/"
    $index = $path.IndexOf($prefix, [StringComparison]::OrdinalIgnoreCase)

    if ($index -lt 0) {
        return $null
    }

    $key = $path.Substring($index + $prefix.Length)
    return [Uri]::UnescapeDataString($key)
}

function New-SignedUrl($objectKey) {
    if ([string]::IsNullOrWhiteSpace($objectKey)) {
        return $null
    }

    $expires = [DateTimeOffset]::UtcNow.AddDays(365).ToUnixTimeSeconds()
    $payload = [Text.Encoding]::UTF8.GetBytes("$objectKey`n$expires")
    $keyBytes = [Text.Encoding]::UTF8.GetBytes($signingSecret)

    $hmac = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
    $sig = [Convert]::ToBase64String($hmac.ComputeHash($payload)).
        TrimEnd("=").
        Replace("+", "-").
        Replace("/", "_")

    $escapedKey = (($objectKey -split "/") | ForEach-Object { [Uri]::EscapeDataString($_) }) -join "/"
    return "/api/storage/signed/$escapedKey?expires=$expires&signature=$([Uri]::EscapeDataString($sig))"
}

function Get-ItemPropertySafe($obj, $name) {
    if ($null -eq $obj) { return $null }
    $prop = $obj.PSObject.Properties[$name]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

$oldUser = @($store.Users)[0]
if ($null -eq $oldUser) {
    throw "No user found in outfit-store.json"
}

$email = $oldUser.Email
$normalizedEmail = $oldUser.NormalizedEmail
if ([string]::IsNullOrWhiteSpace($normalizedEmail)) {
    $normalizedEmail = $email
}

# Берём текущего пользователя из Docker Postgres по email.
# Если его нет, используем старый id из outfit-store.json.
$findUserSql = @"
select id
from users
where lower(coalesce(email, '')) = lower($(SqlText $email))
   or lower(coalesce(normalized_email, '')) = lower($(SqlText $normalizedEmail))
limit 1;
"@

$targetUserId = (& docker compose @composeArgs exec -T postgres psql -U outfit -d outfit_planner -t -A -c $findUserSql).Trim()

if ([string]::IsNullOrWhiteSpace($targetUserId)) {
    $targetUserId = $oldUser.Id
}

Write-Host "Importing old data for user:" $email
Write-Host "Target user id:" $targetUserId

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("begin;")

# User
$lines.Add(@"
insert into users (
    id, email, normalized_email, display_name, password_hash,
    created_at, updated_at, last_login_at, email_verified_at, two_factor_enabled
)
values (
    $(SqlText $targetUserId),
    $(SqlText $oldUser.Email),
    $(SqlText $oldUser.NormalizedEmail),
    $(SqlText $oldUser.DisplayName),
    $(SqlText $oldUser.PasswordHash),
    $(SqlText $oldUser.CreatedAt),
    $(SqlText $oldUser.UpdatedAt),
    $(SqlText $oldUser.LastLoginAt),
    $(SqlText $oldUser.EmailVerifiedAt),
    $(SqlBool $oldUser.TwoFactorEnabled)
)
on conflict (id) do update set
    email = excluded.email,
    normalized_email = excluded.normalized_email,
    display_name = excluded.display_name,
    updated_at = excluded.updated_at,
    last_login_at = excluded.last_login_at;
"@)

# Body photos
foreach ($photo in @($store.BodyPhotos)) {
    $objectKey = Get-ObjectKeyFromSignedUrl $photo.ImageUrl
    $freshUrl = New-SignedUrl $objectKey

    $lines.Add(@"
insert into body_reference_photos (
    id, user_id, image_url, object_key, created_at
)
values (
    $(SqlText $photo.Id),
    $(SqlText $targetUserId),
    $(SqlText $freshUrl),
    $(SqlText $objectKey),
    $(SqlText $photo.CreatedAt)
)
on conflict (id) do update set
    user_id = excluded.user_id,
    image_url = excluded.image_url,
    object_key = excluded.object_key,
    created_at = excluded.created_at;
"@)
}

# Garments
foreach ($g in @($store.Garments)) {
    $imageObjectKey = Get-ObjectKeyFromSignedUrl $g.ImageUrl
    $thumbnailObjectKey = Get-ObjectKeyFromSignedUrl $g.ThumbnailUrl

    if ([string]::IsNullOrWhiteSpace($thumbnailObjectKey)) {
        $thumbnailObjectKey = $imageObjectKey
    }

    $freshImageUrl = New-SignedUrl $imageObjectKey
    $freshThumbnailUrl = New-SignedUrl $thumbnailObjectKey

    $lines.Add(@"
insert into garment_items (
    id, user_id, name, category, body_zone,
    image_url, thumbnail_url,
    object_key, thumbnail_object_key, processed_cutout_object_key,
    tags, primary_color, secondary_colors, material, brand, size,
    season, weather_min_temp, weather_max_temp, occasion,
    formality_score, warmth_score, comfort_score,
    is_favorite, is_archived, last_worn_at, laundry_status, created_at
)
values (
    $(SqlText $g.Id),
    $(SqlText $targetUserId),
    $(SqlText $g.Name),
    $(SqlText $g.Category),
    $(SqlText $g.BodyZone),
    $(SqlText $freshImageUrl),
    $(SqlText $freshThumbnailUrl),
    $(SqlText $imageObjectKey),
    $(SqlText $thumbnailObjectKey),
    $(SqlText $imageObjectKey),
    $(SqlTextArray $g.Tags),
    $(SqlText $g.PrimaryColor),
    $(SqlTextArray $g.SecondaryColors),
    $(SqlText $g.Material),
    $(SqlText $g.Brand),
    $(SqlText $g.Size),
    $(SqlTextArray $g.Season),
    $(SqlInt $g.WeatherMinTemp),
    $(SqlInt $g.WeatherMaxTemp),
    $(SqlTextArray $g.Occasion),
    $(SqlInt $g.FormalityScore),
    $(SqlInt $g.WarmthScore),
    $(SqlInt $g.ComfortScore),
    $(SqlBool $g.IsFavorite),
    $(SqlBool $g.IsArchived),
    $(SqlText $g.LastWornAt),
    $(SqlText $g.LaundryStatus),
    $(SqlText $g.CreatedAt)
)
on conflict (id) do update set
    user_id = excluded.user_id,
    name = excluded.name,
    category = excluded.category,
    body_zone = excluded.body_zone,
    image_url = excluded.image_url,
    thumbnail_url = excluded.thumbnail_url,
    object_key = excluded.object_key,
    thumbnail_object_key = excluded.thumbnail_object_key,
    processed_cutout_object_key = excluded.processed_cutout_object_key,
    tags = excluded.tags,
    primary_color = excluded.primary_color,
    secondary_colors = excluded.secondary_colors,
    material = excluded.material,
    brand = excluded.brand,
    size = excluded.size,
    season = excluded.season,
    weather_min_temp = excluded.weather_min_temp,
    weather_max_temp = excluded.weather_max_temp,
    occasion = excluded.occasion,
    formality_score = excluded.formality_score,
    warmth_score = excluded.warmth_score,
    comfort_score = excluded.comfort_score,
    is_favorite = excluded.is_favorite,
    is_archived = excluded.is_archived,
    last_worn_at = excluded.last_worn_at,
    laundry_status = excluded.laundry_status;
"@)
}

# Outfits + outfit_items
$garmentsById = @{}
foreach ($g in @($store.Garments)) {
    $garmentsById[$g.Id] = $g
}

foreach ($o in @($store.Outfits)) {
    $lines.Add(@"
insert into outfits (
    id, user_id, name, tags, occasion, is_favorite, is_archived,
    clothes_only_preview_url, person_preview_url, created_at
)
values (
    $(SqlText $o.Id),
    $(SqlText $targetUserId),
    $(SqlText $o.Name),
    $(SqlTextArray $o.Tags),
    $(SqlTextArray $o.Occasion),
    $(SqlBool $o.IsFavorite),
    $(SqlBool $o.IsArchived),
    $(SqlText $o.ClothesOnlyPreviewUrl),
    $(SqlText $o.PersonPreviewUrl),
    $(SqlText $o.CreatedAt)
)
on conflict (id) do update set
    user_id = excluded.user_id,
    name = excluded.name,
    tags = excluded.tags,
    occasion = excluded.occasion,
    is_favorite = excluded.is_favorite,
    is_archived = excluded.is_archived,
    clothes_only_preview_url = excluded.clothes_only_preview_url,
    person_preview_url = excluded.person_preview_url;
"@)

    foreach ($item in @($o.Items)) {
        $garmentId = $null
        $category = $null

        if ($item -is [string]) {
            if ($item -match "GarmentId=([^;}\s]+)") {
                $garmentId = $matches[1]
            }
            if ($item -match "Category=([^;}\s]+)") {
                $category = $matches[1]
            }
        } else {
            $garmentId = Get-ItemPropertySafe $item "GarmentId"
            $category = Get-ItemPropertySafe $item "Category"
        }

        if ([string]::IsNullOrWhiteSpace($category) -and $garmentsById.ContainsKey($garmentId)) {
            $category = $garmentsById[$garmentId].Category
        }

        if (-not [string]::IsNullOrWhiteSpace($garmentId) -and -not [string]::IsNullOrWhiteSpace($category)) {
            $lines.Add(@"
insert into outfit_items (outfit_id, garment_id, category)
values (
    $(SqlText $o.Id),
    $(SqlText $garmentId),
    $(SqlText $category)
)
on conflict (outfit_id, garment_id) do update set
    category = excluded.category;
"@)
        }
    }
}

$lines.Add("commit;")

$sqlPath = ".\import-outfit-store.generated.sql"
$lines | Set-Content -Path $sqlPath -Encoding utf8

Write-Host "Generated SQL:" $sqlPath
Get-Content $sqlPath -Raw | docker compose @composeArgs exec -T postgres psql -U outfit -d outfit_planner -v ON_ERROR_STOP=1

Write-Host "Done."