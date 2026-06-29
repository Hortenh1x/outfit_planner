$ErrorActionPreference = "Stop"

$storePath = ".\outfit_planner_back\src\OutfitPlanner.Api\storage\outfit-store.json"
$store = Get-Content $storePath -Raw | ConvertFrom-Json

$composeArgs = @(
  "--env-file", ".env",
  "-f", "docker-compose.dev.yml",
  "-f", "docker-compose.selfhost.override.yml"
)

function SqlText($value) {
    if ($null -eq $value) { return "NULL" }
    $s = [string]$value
    return "'" + ($s -replace "'", "''") + "'"
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("begin;")

foreach ($photo in @($store.BodyPhotos)) {
    $lines.Add(@"
update body_reference_photos
set image_url = $(SqlText $photo.ImageUrl)
where id = $(SqlText $photo.Id);
"@)
}

foreach ($g in @($store.Garments)) {
    $lines.Add(@"
update garment_items
set
    image_url = $(SqlText $g.ImageUrl),
    thumbnail_url = $(SqlText $g.ThumbnailUrl)
where id = $(SqlText $g.Id);
"@)
}

$lines.Add("commit;")

$sqlPath = ".\fix-imported-image-urls.generated.sql"
$lines | Set-Content -Path $sqlPath -Encoding utf8

Get-Content $sqlPath -Raw | docker compose @composeArgs exec -T postgres psql -U outfit -d outfit_planner -v ON_ERROR_STOP=1

Write-Host "Done. Image URLs restored from outfit-store.json."