#purge cdn after deployment
$ResourceGroup = 'fileconversion'
$EndpointName = 'fileconversiontools'
$ProfileName = 'FileConversioNCDN'
Unpublish-AzCdnEndpointContent -ProfileName $ProfileName -ResourceGroupName $ResourceGroup -EndpointName $EndpointName -PurgeContent "/*"