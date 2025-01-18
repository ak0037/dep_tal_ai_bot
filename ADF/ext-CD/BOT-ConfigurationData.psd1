#
# ConfigurationData.psd1
#

@{
    AllNodes = @(
        @{
            NodeName                    = 'LocalHost'
            PSDscAllowPlainTextPassword = $true
            PSDscAllowDomainUser        = $true

            # DisksPresent                = @{DriveLetter = 'F'; DiskID = '2' }

            ServiceSetStopped           = 'ShellHWDetection'

            # IncludesAllSubfeatures
            WindowsFeaturePresent2      = 'RSAT'

            # given this is for a lab and load test, just always pull down the latest App config
            DSCConfigurationMode        = 'ApplyAndAutoCorrect'

            DisableIEESC                = $True

            FWRules                     = @(
                @{
                    Name      = 'PsiBot'
                    LocalPort = ('8445', '9442', '9441')
                }
            )
            
            DirectoryPresent            = @(
                'C:\Source\InstallLogs', 'C:\API\PsiBot', 'C:\Build\PsiBot'
            )
            
            # Port Mappings from NAT Pools on Azure Load Balancer
            # Dynamically set from Azure Metadata Service
            EnvironmentVarPresentVMSS   = @(
                @{
                    Name             = 'PsiBot:Service:Settings:BotConfiguration:MediaInstanceExternalPort'
                    BackendPortMatch = '8445'
                    Value            = '{0}'
                },
                @{
                    Name             = 'PsiBot:Service:Settings:BotConfiguration:BotInstanceExternalPort'
                    BackendPortMatch = '9441'
                    Value            = '{0}'
                }
            )

            # default environment variables
            EnvironmentVarPresent       = @(
                @{
                    Name  = 'PsiBot:Service:Settings:BotConfiguration:BotCallingInternalPort'
                    Value = '9442'
                },
                @{
                    Name  = 'PsiBot:Service:Settings:BotConfiguration:CallSignalingPort'
                    Value = '9441'
                },
                @{
                    Name  = 'Psibot.ServiceBotConfiguration:InstanceInternalPort'
                    Value = '8445'
                }
            )
            
            EnvironmentVarSet = @(
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'BotName' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'ServiceDnsName' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'ServiceCname' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'CertificateThumbprint' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'AadAppId' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'OrgName' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'AadAppSecret' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'MediaServiceFQDN' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'PsiStoreDirectory' },
                @{Prefix = 'PsiBot:Service:Settings:BotConfiguration:'; KVName = '{0}-kv'; Name = 'PlaceCallEndpointUrl' }
            )

            # Blob copy with Managed Identity - Oauth2
            # Commented out for Lab, using RemoteFilePresent instead
            # AZCOPYDSCDirPresentSource  = @(
            #     @{
            #         SourcePathBlobURI = 'https://{0}.blob.core.windows.net/source/GIT/'
            #         DestinationPath   = 'C:\Source\GIT\'
            #     },
            #     @{
            #         SourcePathBlobURI = 'https://{0}.blob.core.windows.net/source/dotnet/'
            #         DestinationPath   = 'C:\Source\dotnet\'
            #     },
            #     @{
            #         SourcePathBlobURI = 'https://{0}.blob.core.windows.net/source/VisualStudio/'
            #         DestinationPath   = 'C:\Source\VisualStudio\'
            #     }
            # )

            # this downloads the files each time, you can use AZCOPYDSCDirPresentSource above
            # As an alternative if you stage the files
            RemoteFilePresent           = @(
                @{
                    Uri             = 'https://github.com/git-for-windows/git/releases/download/v2.42.0.windows.2/Git-2.42.0.2-64-bit.exe'
                    DestinationPath = 'C:\Source\GIT\Git-2.42.0.2-64-bit.exe'
                },
                @{
                    Uri             = 'https://aka.ms/vs/17/release/vc_redist.x64.exe'
                    DestinationPath = 'C:\Source\dotnet\vc_redist.x64.exe'
                },
                @{
                    Uri             = 'https://aka.ms/vs/17/release/vs_enterprise.exe'
                    DestinationPath = 'C:\Source\VisualStudio\vs_enterprise.exe'
                }
            )

            # The bot needs Microsoft Visual C++ 2015-2022 Redistributable (x64) to be installed on the VM.
            # Visual Studio 2022 installs this software as part of the installation.
            # If you are doing a production deployment, comment out the Visual Studio Installation
            # and uncomment the Microsoft Visual C++ 2015-2022 Redistributable (x64) installation.
            # NOTE: If you are installing the C++ redistruable separately, you can see that the version number
            # is part of the name. When this package is updated periodically and gets a new version number
            # the install could cause a failure in the DSC configuration. If this happens, we wil need to update
            # the version number in the name of the package.
            SoftwarePackagePresent      = @(
                @{
                    Name      = 'Git'
                    Path      = 'C:\Source\GIT\Git-2.42.0.2-64-bit.exe'
                    ProductId = ''
                    Arguments = '/VERYSILENT'
                },
                # @{
                #     Name      = 'Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.38.33130'
                #     Path      = 'C:\Source\dotnet\vc_redist.x64.exe'
                #     ProductId = ''
                #     Arguments = '/install /q /norestart'
                # },
                @{  
                    Name      = 'Visual Studio Enterprise 2022'
                    Path      = 'C:\Source\VisualStudio\vs_enterprise.exe'
                    ProductId = ''
                    Arguments = '--installPath C:\VisualStudio\2022\Enterprise --addProductLang en-US --add Microsoft.VisualStudio.Workload.ManagedDesktop --includeRecommended --quiet --wait --norestart' #--config "C:\Source\VisualStudio\.vsconfig"
                }
            )

            # Blob copy with Managed Identity - Oauth2
            AppReleaseDSCAppPresent     = @(
                @{
                    ComponentName     = 'PsiBot'
                    SourcePathBlobURI = 'https://{0}.blob.core.windows.net/builds/'
                    DestinationPath   = 'C:\API\'
                    ValidateFileName  = 'CurrentBuild.txt'
                    BuildFileName     = 'C:\Build\PsiBot\componentBuild.json'
                    SleepTime         = '10'
                }
            )

            NewServicePresent           = @(
                @{
                    Name        = 'Psi Bot Service'
                    Path        = 'C:\API\PsiBot\PsiBot.Service.exe'
                    State       = 'Running'
                    StartupType = 'Automatic'
                    Description = 'Psi Bot Service'
                }
            )

            CertificatePortBinding      = @(
                @{
                    Name  = 'MediaControlPlane'
                    Port  = '8445'
                    AppId = '{7c64d8a0-4cbb-42b6-85a8-de0e00f6a9c6}'
                },
                @{
                    Name  = 'BotCalling'
                    Port  = '9442'
                    AppId = '{7c64d8a0-4cbb-42b6-85a8-de0e00f6a9c6}'
                },
                @{
                    Name  = 'BotNotification'
                    Port  = '9441'
                    AppId = '{7c64d8a0-4cbb-42b6-85a8-de0e00f6a9c6}'
                }
            )
        }
    )
}
