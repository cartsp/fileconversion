$StorageAccountName = "fileconversiontools"
$StorageAccountKey = "5LMlKtgZh/lmzyEVM+GzWQ6WDHtvRUCZBP67WqJgokd5q71AK2UMDday5Lvu9UAQkQjELH141com5EeRvFLJSg==" 

$Context = New-AzStorageContext -StorageAccountName $StorageAccountName -StorageAccountKey $StorageAccountKey

#purge cdn after deployment
$ResourceGroup = 'fileconversion'
$EndpointName = 'fileconversiontools'
$ProfileName = 'FileConversioNCDN'
Unpublish-AzCdnEndpointContent -ProfileName $ProfileName -ResourceGroupName $ResourceGroup -EndpointName $EndpointName -PurgeContent "/*"