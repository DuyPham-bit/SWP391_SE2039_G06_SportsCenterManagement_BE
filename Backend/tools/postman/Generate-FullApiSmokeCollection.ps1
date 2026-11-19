param(
    [string] $BaseUrl = "http://localhost:54162",
    [string] $SwaggerUrl = "$BaseUrl/swagger/v1/swagger.json"
)

$ErrorActionPreference = "Stop"
$swagger = Invoke-RestMethod -Uri $SwaggerUrl -Method Get
$itemsByTag = [ordered]@{}
$environmentValues = [ordered]@{
    baseUrl = $BaseUrl.TrimEnd('/')
    token = ""
}
$operationCount = 0
$supportedMethods = @("get", "post", "put", "patch", "delete", "options", "head", "trace")

foreach ($pathEntry in $swagger.paths.PSObject.Properties) {
    $pathTemplate = [regex]::Replace($pathEntry.Name, "\{([^}:]+)(?::[^}]+)?\}", '{{$1}}')
    foreach ($methodEntry in $pathEntry.Value.PSObject.Properties) {
        $method = $methodEntry.Name.ToLowerInvariant()
        if ($method -notin $supportedMethods) { continue }

        $operation = $methodEntry.Value
        $tag = if ($operation.tags -and $operation.tags.Count -gt 0) { [string]$operation.tags[0] } else { "Other" }
        if (-not $itemsByTag.Contains($tag)) { $itemsByTag[$tag] = [System.Collections.Generic.List[object]]::new() }

        $url = "{{baseUrl}}$pathTemplate"
        $parameters = @()
        if ($pathEntry.Value.parameters) { $parameters += @($pathEntry.Value.parameters) }
        if ($operation.parameters) { $parameters += @($operation.parameters) }

        $queryParts = [System.Collections.Generic.List[string]]::new()
        foreach ($parameter in $parameters) {
            $name = [string]$parameter.name
            if (-not $name) { continue }
            $schema = $parameter.schema
            $default = "__smoke_test__"
            if ($name -match "(?i)id$") { $default = "0" }
            if ($parameter.in -eq "path") {
                if ($schema.type -in @("integer", "number") -or $name -match "(?i)(id|code)$") {
                    $default = "0"
                } elseif ($schema.format -eq "date" -or $name -match "(?i)date$") {
                    $default = "2026-01-01"
                } elseif ($schema.format -eq "date-time") {
                    $default = "2026-01-01T00:00:00Z"
                }
                $environmentValues[$name] = $default
            } elseif ($parameter.in -eq "query") {
                if ($name -match "(?i)id$") { $default = "0" }
                elseif ($schema.type -in @("integer", "number")) { $default = "1" }
                elseif ($schema.format -eq "date") { $default = "2026-01-01" }
                elseif ($schema.format -eq "date-time") { $default = "2026-01-01T00:00:00Z" }
                $environmentValues[$name] = $default
                $escapedName = [uri]::EscapeDataString($name)
                $queryParts.Add("$escapedName={{$name}}")
            }
        }
        if ($queryParts.Count -gt 0) { $url += "?" + ($queryParts -join "&") }

        $headers = @(@{ key = "Accept"; value = "application/json"; type = "text" })
        $request = [ordered]@{
            method = $method.ToUpperInvariant()
            header = $headers
            url = $url
            description = "Generated from the API OpenAPI document. Empty or safe placeholder values are used; 4xx responses are expected for missing authentication, invalid IDs, or incomplete payloads. Add a Bearer token for protected routes."
        }
        if ($method -in @("post", "put", "patch")) {
            $request.header += @{ key = "Content-Type"; value = "application/json"; type = "text" }
            $request.body = @{ mode = "raw"; raw = "{}"; options = @{ raw = @{ language = "json" } } }
        }

        $testScript = @(
            "pm.test('Route does not return a server error', function () {",
            "    pm.expect(pm.response.code).to.be.below(500);",
            "});"
        )
        $itemsByTag[$tag].Add([ordered]@{
            name = "$($method.ToUpperInvariant()) $pathTemplate"
            request = $request
            response = @()
            event = @(@{ listen = "test"; script = @{ type = "text/javascript"; exec = $testScript } })
        })
        $operationCount++
    }
}

$collectionItems = [System.Collections.Generic.List[object]]::new()
foreach ($tag in $itemsByTag.Keys) {
    $collectionItems.Add(@{ name = $tag; item = @($itemsByTag[$tag].ToArray()) })
}
$now = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$collection = [ordered]@{
    info = @{
        _postman_id = [guid]::NewGuid().ToString()
        name = "Sports Center Management API - Full Route Smoke"
        description = "Generated from the running API OpenAPI document. Contains $operationCount operations. 4xx is allowed for safe invalid placeholders or missing auth; review state-changing operations and set authorized data before sending."
        schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
    }
    item = @($collectionItems.ToArray())
}
$environment = [ordered]@{
    id = [guid]::NewGuid().ToString()
    name = "Sports Center Management - Full Route Smoke"
    values = @($environmentValues.Keys | ForEach-Object {
        @{ key = $_; value = [string]$environmentValues[$_]; type = $(if ($_ -eq "token") { "secret" } else { "default" }); enabled = $true }
    })
    _postman_variable_scope = "environment"
    _postman_exported_at = $now
    _postman_exported_using = "Generate-FullApiSmokeCollection.ps1"
}

$outputDirectory = $PSScriptRoot
$collectionPath = Join-Path $outputDirectory "SportsCenterManagement.full.postman_collection.json"
$environmentPath = Join-Path $outputDirectory "SportsCenterManagement.full.postman_environment.json"
$collection | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $collectionPath -Encoding utf8
$environment | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $environmentPath -Encoding utf8
Write-Output "Generated $operationCount operations: $collectionPath"
Write-Output "Generated environment: $environmentPath"
