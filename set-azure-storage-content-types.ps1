If(-not(Get-InstalledModule -Name Az -ErrorAction silentlycontinue)){
    Install-Module -Name Az -AllowClobber -Scope CurrentUser -Confirm:$False -Force
}

$StorageAccountName = "fileconversiontools"
<#
$StorageAccountKey = "5LMlKtgZh/lmzyEVM+GzWQ6WDHtvRUCZBP67WqJgokd5q71AK2UMDday5Lvu9UAQkQjELH141com5EeRvFLJSg==" 
#>
$StorageAccountKey = "ywQ6NZ67vWsJZ0Nn1J8QTs3a8HHcwGGVYdIrivMYsOLcmQqRRrGM8wvA2lX+cRF5FdWx2joXkUS42AEId4A9DQ==" 
$ContainerName = "`$web"

$Context = New-AzStorageContext -StorageAccountName $StorageAccountName -StorageAccountKey $StorageAccountKey
$Blobs = Get-AzStorageBlob -Context $Context -Container $ContainerName

foreach ($Blob in $Blobs) 
{  
    $Extn = [IO.Path]::GetExtension($Blob.Name)
    $ContentType = ""

    switch ($Extn) {
        ".json" { $ContentType = "application/json" }
        ".js" { $ContentType = "application/javascript" }
        ".svg" { $ContentType = "image/svg+xml" }
        ".dll" { $ContentType = "application/octet-stream" }
        ".wasm" { $ContentType = "application/wasm" }
        ".html" { $ContentType = "text/html" }
        ".css" { $ContentType = "text/css" }
        ".map" { $ContentType = "text/plain" }
        ".md" { $ContentType = "text/plain" }
        ".eot" { $ContentType = "application/vnd.ms-fontobject" }
        ".otf" { $ContentType = "font/otf" }
        ".svg" { $ContentType = "image/svg+xml" }
        ".ttf" { $ContentType = "font/ttf" }
        ".woff" { $ContentType = "font/woff" }

        Default { $ContentType = "" }
    }
    $CloudBlockBlob = [Microsoft.Azure.Storage.Blob.CloudBlockBlob] $Blob.ICloudBlob
    if ($ContentType -ne "") {
        $CloudBlockBlob.Properties.ContentType = $ContentType    
    }
    $CloudBlockBlob.Properties.ContentEncoding = 'gzip' 
    $CloudBlockBlob.Properties.CacheControl = 'max-age=31536000' 

    $CloudBlockBlob.SetProperties()    
}

#purge cdn after deployment
$ResourceGroup = 'fileconversion'
$EndpointName = 'fileconversiontools'
$ProfileName = 'FileConversioNCDN'
Unpublish-AzCdnEndpointContent -ProfileName $ProfileName -ResourceGroupName $ResourceGroup -EndpointName $EndpointName -PurgeContent "/*"

#remove dist directory now we are finished with it so its clean for next run
Remove-Item -Recurse -Force FileConvert\bin\Release\netstandard2.1\publish\wwwroot -ErrorAction SilentlyContinue