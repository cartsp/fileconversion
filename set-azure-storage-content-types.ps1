If(-not(Get-InstalledModule -Name Az -ErrorAction silentlycontinue)){
    Install-Module -Name Az -AllowClobber -Scope CurrentUser -Confirm:$False -Force
}

$StorageAccountName = "devfileconversion"
$StorageAccountKey = "rgYcOvyRUc7uMn/xwZUsrJ/bDnp49EjzgicI/bgnf2XL9LFenHEger6VhqaRboz/9a1KtbgYu7zXSdfJgXuOeQ==" 
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
#remove dist directory now we are finished with it so its clean for next run
Remove-Item -Recurse -Force FileConvert\bin\Release\netstandard2.1\publish\FileConvert\dist -ErrorAction SilentlyContinue