If(-not(Get-InstalledModule -Name Az -ErrorAction silentlycontinue)){
    Install-Module -Name Az -AllowClobber -Scope CurrentUser -Confirm:$False -Force
}

$resourceGroup = "fileconversion"
$storageAccountName = "devfileconversion"

$storageAccount = Get-AzStorageAccount -ResourceGroupName $resourceGroup -Name $storageAccountName


$StorageAccountName = "devfileconversion" # i.e. WolfTrackerStorage
# If you're using VSTS I would strongly suggest using Key Vault to store and retrieve the key. Keep secrets out of your code!
$StorageAccountKey = "rgYcOvyRUc7uMn/xwZUsrJ/bDnp49EjzgicI/bgnf2XL9LFenHEger6VhqaRboz/9a1KtbgYu7zXSdfJgXuOeQ==" 
$ContainerName = "`$web"  # i.e. wolfpics

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
    $CloudBlockBlob.SetProperties()    
}