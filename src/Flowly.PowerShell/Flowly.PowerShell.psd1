@{
    RootModule           = 'Flowly.PowerShell.dll'
    # Placeholder: the release workflow overwrites ModuleVersion and Prerelease with the version of the release tag.
    ModuleVersion        = '0.0.0'
    GUID                 = 'b3c0b6ea-9a2f-4c9f-9b2b-2f7f7a2f3b10'
    Author               = 'Rasmus Therkelsen'
    CompanyName          = 'Flowly'
    Copyright            = 'Copyright © Rasmus Therkelsen'
    Description          = 'A thin client for the Flowly Management API.'
    PowerShellVersion    = '7.4'
    FunctionsToExport    = @()
    CmdletsToExport      = @(
        'Connect-Flowly',
        'Disconnect-Flowly',
        'Get-FlowlyMessageStreamCheckpoint',
        'Reset-FlowlyMessageStreamCheckpoint',
        'Get-FlowlyMessageStreamPartition',
        'Resume-FlowlyMessageStreamPartition'
    )
    VariablesToExport    = @()
    AliasesToExport      = @()
    PrivateData          = @{
        PSData = @{
            Tags       = @('Flowly', 'Messaging', 'RabbitMQ')
            ProjectUri = 'https://github.com/rasmustherkelsen/flowly'
            Prerelease = ''
        }
    }
}
