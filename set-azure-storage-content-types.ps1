If(-not(Get-InstalledModule -Name Az -ErrorAction silentlycontinue)){
    Install-Module -Name Az -AllowClobber -Scope CurrentUser -Confirm:$False -Force
}

$resourceGroup = "fileconversion"
$storageAccountName = "devfileconversion"

$storageAccount = Get-AzureStorageAccount -ResourceGroupName $resourceGroup -Name $storageAccountName


$StorageAccountName = "devfileconversion" # i.e. WolfTrackerStorage
# If you're using VSTS I would strongly suggest using Key Vault to store and retrieve the key. Keep secrets out of your code!
$StorageAccountKey = "rgYcOvyRUc7uMn/xwZUsrJ/bDnp49EjzgicI/bgnf2XL9LFenHEger6VhqaRboz/9a1KtbgYu7zXSdfJgXuOeQ==" 
$ContainerName = "`$web"  # i.e. wolfpics

$Context = New-AzureStorageContext -StorageAccountName $StorageAccountName -StorageAccountKey $StorageAccountKey
$Blobs = Get-AzureStorageBlob -Context $Context -Container $ContainerName

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

        Default { $ContentType = "" }
    }
    $CloudBlockBlob = [Microsoft.WindowsAzure.Storage.Blob.CloudBlockBlob] $Blob.ICloudBlob
    if ($ContentType -ne "") {
        $CloudBlockBlob.Properties.ContentType = $ContentType    
    }
    $CloudBlockBlob.Properties.ContentEncoding = 'gzip' 
    $CloudBlockBlob.SetProperties()    
}