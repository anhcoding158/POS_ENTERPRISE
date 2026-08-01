#requires -Version 5.1

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Exit-code contract and precedence:
#   10 (unexpected fatal) > 2 (repository/path/Git precondition) >
#   5 (manifest/hash) > 4 (security finding) > 3 (inventory/coverage) > 0.
# Repository and unexpected failures stop processing immediately. When a pack can
# be completed, manifest failure takes precedence over security, which takes
# precedence over inventory/coverage failure.
$script:ExitSuccess = 0
$script:ExitRepositoryFailure = 2
$script:ExitInventoryFailure = 3
$script:ExitSecurityFinding = 4
$script:ExitManifestFailure = 5
$script:ExitUnexpectedFailure = 10

$script:Utf8NoBom =
    New-Object `
        -TypeName System.Text.UTF8Encoding `
        -ArgumentList $false, $true
$script:Utf8Strict =
    New-Object `
        -TypeName System.Text.UTF8Encoding `
        -ArgumentList $false, $true
$script:Utf16LittleEndianStrict =
    New-Object `
        -TypeName System.Text.UnicodeEncoding `
        -ArgumentList $false, $false, $true
$script:Utf16BigEndianStrict =
    New-Object `
        -TypeName System.Text.UnicodeEncoding `
        -ArgumentList $true, $false, $true

[Console]::OutputEncoding = $script:Utf8NoBom
$OutputEncoding = $script:Utf8NoBom

$script:SecurityFindings =
    New-Object 'System.Collections.Generic.List[object]'

function Throw-ExportFailure
{
    param(
        [Parameter(Mandatory = $true)]
        [int]$ExitCode,

        [Parameter(Mandatory = $true)]
        [string]$SafeMessage
    )

    $exception =
        New-Object `
            -TypeName System.Exception `
            -ArgumentList $SafeMessage
    $exception.Data['ProjectContextExitCode'] = $ExitCode
    throw $exception
}

function Get-NormalizedFullPath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath
    )

    return [System.IO.Path]::GetFullPath($LiteralPath).
        TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar
        )
}

function Test-FullPathWithinRoot
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$CandidatePath,

        [switch]$AllowRoot
    )

    $normalizedRoot = Get-NormalizedFullPath -LiteralPath $RootPath
    $normalizedCandidate = Get-NormalizedFullPath -LiteralPath $CandidatePath

    if (
        [string]::Equals(
            $normalizedRoot,
            $normalizedCandidate,
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        return $AllowRoot.IsPresent
    }

    $rootPrefix =
        $normalizedRoot +
        [System.IO.Path]::DirectorySeparatorChar

    return $normalizedCandidate.StartsWith(
        $rootPrefix,
        [System.StringComparison]::OrdinalIgnoreCase
    )
}

function ConvertFrom-GitRelativePath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$GitPath
    )

    if ([string]::IsNullOrWhiteSpace($GitPath))
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'Git returned an empty repository-relative path.'
    }

    if ($GitPath.IndexOf([char]0) -ge 0)
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'Git returned a path containing an embedded NUL.'
    }

    $relativePath = $GitPath.Replace('\', '/')

    if (
        [System.IO.Path]::IsPathRooted($relativePath) -or
        $relativePath.Contains(':')
    )
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'Git returned a rooted or drive-qualified path.'
    }

    $segments = $relativePath.Split('/')
    foreach ($segment in $segments)
    {
        if (
            [string]::IsNullOrEmpty($segment) -or
            $segment -eq '.' -or
            $segment -eq '..'
        )
        {
            Throw-ExportFailure `
                -ExitCode $script:ExitRepositoryFailure `
                -SafeMessage 'Git returned a path with an unsafe segment.'
        }
    }

    $platformRelativePath =
        $relativePath.Replace(
            '/',
            [System.IO.Path]::DirectorySeparatorChar
        )

    $absolutePath =
        [System.IO.Path]::GetFullPath(
            [System.IO.Path]::Combine(
                $RepositoryRoot,
                $platformRelativePath
            )
        )

    if (
        -not (
            Test-FullPathWithinRoot `
                -RootPath $RepositoryRoot `
                -CandidatePath $absolutePath
        )
    )
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'A Git path resolved outside the repository root.'
    }

    return [pscustomobject]@{
        RelativePath = $relativePath
        AbsolutePath = $absolutePath
    }
}

function Get-RelativePathFromRoot
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$AbsolutePath
    )

    $normalizedRoot = Get-NormalizedFullPath -LiteralPath $RootPath
    $normalizedPath = Get-NormalizedFullPath -LiteralPath $AbsolutePath

    if (
        -not (
            Test-FullPathWithinRoot `
                -RootPath $normalizedRoot `
                -CandidatePath $normalizedPath
        )
    )
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'A requested relative path is outside its root.'
    }

    $rootPrefix =
        $normalizedRoot +
        [System.IO.Path]::DirectorySeparatorChar

    return $normalizedPath.
        Substring($rootPrefix.Length).
        Replace('\', '/')
}

function ConvertTo-SafeReportedPath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $safeSegments = New-Object 'System.Collections.Generic.List[string]'

    foreach ($segment in $RelativePath.Replace('\', '/').Split('/'))
    {
        $safeSegment =
            [System.Text.RegularExpressions.Regex]::Replace(
                $segment,
                '[\x00-\x1F\x7F]',
                '?'
            )

        if (
            $safeSegment -match
                '(?i)[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}' -or
            $safeSegment -match '(?<!\d)\d{8,}(?!\d)'
        )
        {
            $safeSegment = '[REDACTED-PATH-SEGMENT]'
        }

        $safeSegments.Add($safeSegment)
    }

    return [string]::Join('/', $safeSegments.ToArray())
}

function Invoke-GitCapture
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$Operation,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $capturedOutput = & {
        $ErrorActionPreference = 'Continue'
        & git -C $RepositoryRoot @Arguments 2>&1
    }
    $capturedExitCode = $LASTEXITCODE

    if ($capturedExitCode -ne 0)
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage (
                'Git operation failed: {0}; exit code {1}. ' +
                'Captured output is intentionally not echoed.'
            ) -f $Operation, $capturedExitCode
    }

    $text = ''
    if ($null -ne $capturedOutput)
    {
        if ($capturedOutput -is [System.Array])
        {
            $stringLines = New-Object 'System.Collections.Generic.List[string]'
            foreach ($line in $capturedOutput)
            {
                if ($line -is [System.Management.Automation.ErrorRecord])
                {
                    continue
                }

                $stringLines.Add([string]$line)
            }

            $text =
                [string]::Join(
                    [System.Environment]::NewLine,
                    $stringLines.ToArray()
                )
        }
        elseif ($capturedOutput -is [System.Management.Automation.ErrorRecord])
        {
            $text = ''
        }
        else
        {
            $text = [string]$capturedOutput
        }
    }

    return [pscustomobject]@{
        Operation = $Operation
        ExitCode = $capturedExitCode
        Text = $text
    }
}

function Split-NulTerminatedGitPaths
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$CapturedText
    )

    $paths = New-Object 'System.Collections.Generic.List[string]'

    if ($CapturedText.Length -eq 0)
    {
        return ,$paths.ToArray()
    }

    foreach ($path in $CapturedText.Split([char]0))
    {
        if ($path.Length -gt 0)
        {
            $paths.Add($path)
        }
    }

    return ,$paths.ToArray()
}

function Get-Sha256Hex
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]]$Bytes
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        $hashBytes = $sha256.ComputeHash($Bytes)
        return (
            [System.BitConverter]::ToString($hashBytes).
            Replace('-', '').
            ToLowerInvariant()
        )
    }
    finally
    {
        $sha256.Dispose()
    }
}

function Read-ArtifactBytes
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath
    )

    return ,[System.IO.File]::ReadAllBytes($LiteralPath)
}

function Write-Utf8Artifact
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content,

        [switch]$AllowReplace
    )

    if (
        (Test-Path -LiteralPath $LiteralPath) -and
        -not $AllowReplace.IsPresent
    )
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'An exporter artifact unexpectedly already exists.'
    }

    [System.IO.File]::WriteAllText(
        $LiteralPath,
        $Content,
        $script:Utf8NoBom
    )
}

function ConvertTo-CsvText
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Rows
    )

    if ($Rows.Count -eq 0)
    {
        return ''
    }

    $csvLines = $Rows | ConvertTo-Csv -NoTypeInformation
    return (
        [string]::Join(
            [System.Environment]::NewLine,
            [string[]]$csvLines
        ) +
        [System.Environment]::NewLine
    )
}

function Get-LineNumberAtIndex
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [int]$Index
    )

    $lineNumber = 1
    $limit = [Math]::Min($Index, $Text.Length)

    for ($position = 0; $position -lt $limit; $position++)
    {
        if ($Text[$position] -eq [char]10)
        {
            $lineNumber++
        }
    }

    return $lineNumber
}

function Add-SecurityFinding
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuleId,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [AllowNull()]
        [Nullable[int]]$LineNumber,

        [Parameter(Mandatory = $true)]
        [string]$Source
    )

    $safePath = ConvertTo-SafeReportedPath -RelativePath $RelativePath
    $lineText = ''
    if ($null -ne $LineNumber)
    {
        $lineText = [string]$LineNumber
    }

    $script:SecurityFindings.Add(
        [pscustomobject]@{
            RuleId = $RuleId
            RelativePath = $safePath
            LineNumber = $lineText
            Source = $Source
        }
    )
}

function Test-IsDocumentationOrTestPath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $normalizedPath = $RelativePath.Replace('\', '/')
    $extension =
        [System.IO.Path]::GetExtension($normalizedPath).ToLowerInvariant()

    return (
        $normalizedPath.StartsWith(
            'tests/',
            [System.StringComparison]::OrdinalIgnoreCase
        ) -or
        $normalizedPath.StartsWith(
            'docs/',
            [System.StringComparison]::OrdinalIgnoreCase
        ) -or
        $extension -eq '.md'
    )
}

function Test-IsPlaceholderValue
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,

        [switch]$WasQuoted
    )

    $candidate = $Value.Trim()

    if ($candidate.Length -eq 0)
    {
        return $true
    }

    if (
        $candidate -match
            '(?i)^(null|none|default|example|sample|dummy|fake|' +
            'synthetic|placeholder|redacted|not[-_ ]?set|' +
            'change[-_ ]?me|your[-_ ].*|test[-_ ].*|' +
            'development|localhost|true|false|x+|\*+|\.+)$'
    )
    {
        return $true
    }

    if (
        $candidate.StartsWith('${') -or
        $candidate.StartsWith('$env:') -or
        $candidate.StartsWith('$(') -or
        $candidate.StartsWith('%') -or
        $candidate.StartsWith('<') -or
        $candidate.Contains('[REDACTED:')
    )
    {
        return $true
    }

    if (
        -not $WasQuoted.IsPresent -and
        (
            $candidate -match '^[A-Za-z_][A-Za-z0-9_]*$' -or
            $candidate -match
                '^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+$'
        )
    )
    {
        return $true
    }

    if (
        $candidate.Contains('::') -or
        $candidate.Contains('=>') -or
        $candidate.Contains('\k<') -or
        $candidate.Contains('(?<')
    )
    {
        return $true
    }

    if (
        -not $WasQuoted.IsPresent -and
        (
            $candidate.StartsWith('{') -or
            $candidate -match '^[A-Za-z_][A-Za-z0-9_.]*\s*[\[(<]'
        )
    )
    {
        return $true
    }

    return $false
}

function Replace-MatchGroup
{
    param(
        [Parameter(Mandatory = $true)]
        [System.Text.RegularExpressions.Match]$Match,

        [Parameter(Mandatory = $true)]
        [System.Text.RegularExpressions.Group]$Group,

        [Parameter(Mandatory = $true)]
        [string]$Replacement
    )

    $relativeStart = $Group.Index - $Match.Index
    $suffixStart = $relativeStart + $Group.Length

    return (
        $Match.Value.Substring(0, $relativeStart) +
        $Replacement +
        $Match.Value.Substring($suffixStart)
    )
}

function Protect-TextContent
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$FindingSource
    )

    $state = [pscustomobject]@{
        Count = 0
    }

    $sanitizedText = $Text
    if ($FindingSource -eq 'TrackedCurrentDiff')
    {
        $sanitizedText =
            [regex]::Replace(
                $sanitizedText,
                '(?m)^-(?!-).*(?:\r\n|\r|\n|$)',
                ''
            )
    }

    $documentationOrTest =
        Test-IsDocumentationOrTestPath -RelativePath $RelativePath

    $privateKeyPattern =
        '(?ms)-----BEGIN (?<type>(?:RSA |EC |DSA |OPENSSH )?' +
        'PRIVATE KEY)-----.*?-----END \k<type>-----'

    $privateKeyRegex =
        New-Object `
            -TypeName System.Text.RegularExpressions.Regex `
            -ArgumentList $privateKeyPattern

    $privateKeyEvaluator =
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)

            $lineNumber =
                Get-LineNumberAtIndex `
                    -Text $sanitizedText `
                    -Index $match.Index

            Add-SecurityFinding `
                -RuleId 'SEC-PRIVATE-KEY-BLOCK' `
                -RelativePath $RelativePath `
                -LineNumber $lineNumber `
                -Source $FindingSource

            $state.Count++
            $lineBreaks =
                [System.Text.RegularExpressions.Regex]::Matches(
                    $match.Value,
                    '\r\n|\r|\n'
                )

            $replacement =
                New-Object `
                    -TypeName System.Text.StringBuilder `
                    -ArgumentList '[REDACTED:PRIVATE_KEY]'

            foreach ($lineBreak in $lineBreaks)
            {
                [void]$replacement.Append($lineBreak.Value)
            }

            return $replacement.ToString()
        }

    $sanitizedText =
        $privateKeyRegex.Replace(
            $sanitizedText,
            $privateKeyEvaluator
        )

    $bearerPattern =
        '(?i)(?<prefix>\bBearer\s+)' +
        '(?<value>[A-Za-z0-9][A-Za-z0-9._~+/\-=]{15,})'

    $bearerRegex =
        New-Object `
            -TypeName System.Text.RegularExpressions.Regex `
            -ArgumentList $bearerPattern

    $bearerEvaluator =
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)

            $value = $match.Groups['value'].Value
            if (Test-IsPlaceholderValue -Value $value)
            {
                return $match.Value
            }

            $lineNumber =
                Get-LineNumberAtIndex `
                    -Text $sanitizedText `
                    -Index $match.Index

            Add-SecurityFinding `
                -RuleId 'SEC-BEARER-TOKEN' `
                -RelativePath $RelativePath `
                -LineNumber $lineNumber `
                -Source $FindingSource

            $state.Count++
            return (
                Replace-MatchGroup `
                    -Match $match `
                    -Group $match.Groups['value'] `
                    -Replacement '[REDACTED:BEARER_TOKEN]'
            )
        }

    $sanitizedText =
        $bearerRegex.Replace(
            $sanitizedText,
            $bearerEvaluator
        )

    $knownTokenPattern =
        '(?<![A-Za-z0-9])(?<value>' +
        '(?:sk_(?:live|test)_[A-Za-z0-9]{16,})|' +
        '(?:sk-[A-Za-z0-9_-]{20,})|' +
        '(?:ghp_[A-Za-z0-9]{20,})|' +
        '(?:github_pat_[A-Za-z0-9_]{20,})|' +
        '(?:AKIA[A-Z0-9]{16})|' +
        '(?:AIza[A-Za-z0-9_-]{20,})|' +
        '(?:xox[baprs]-[A-Za-z0-9-]{16,})' +
        ')(?![A-Za-z0-9])'

    $knownTokenRegex =
        New-Object `
            -TypeName System.Text.RegularExpressions.Regex `
            -ArgumentList $knownTokenPattern

    $knownTokenEvaluator =
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)

            $lineNumber =
                Get-LineNumberAtIndex `
                    -Text $sanitizedText `
                    -Index $match.Index

            Add-SecurityFinding `
                -RuleId 'SEC-KNOWN-TOKEN-FORMAT' `
                -RelativePath $RelativePath `
                -LineNumber $lineNumber `
                -Source $FindingSource

            $state.Count++
            return (
                Replace-MatchGroup `
                    -Match $match `
                    -Group $match.Groups['value'] `
                    -Replacement '[REDACTED:KNOWN_TOKEN]'
            )
        }

    $sanitizedText =
        $knownTokenRegex.Replace(
            $sanitizedText,
            $knownTokenEvaluator
        )

    $jwtPattern =
        '(?<![A-Za-z0-9_-])(?<value>' +
        'eyJ[A-Za-z0-9_-]{8,}\.' +
        '[A-Za-z0-9_-]{8,}\.' +
        '[A-Za-z0-9_-]{8,}' +
        ')(?![A-Za-z0-9_-])'

    $jwtRegex =
        New-Object `
            -TypeName System.Text.RegularExpressions.Regex `
            -ArgumentList $jwtPattern

    $jwtEvaluator =
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)

            if ($documentationOrTest)
            {
                return $match.Value
            }

            $lineNumber =
                Get-LineNumberAtIndex `
                    -Text $sanitizedText `
                    -Index $match.Index

            Add-SecurityFinding `
                -RuleId 'SEC-JWT-VALUE' `
                -RelativePath $RelativePath `
                -LineNumber $lineNumber `
                -Source $FindingSource

            $state.Count++
            return (
                Replace-MatchGroup `
                    -Match $match `
                    -Group $match.Groups['value'] `
                    -Replacement '[REDACTED:JWT]'
            )
        }

    $sanitizedText =
        $jwtRegex.Replace(
            $sanitizedText,
            $jwtEvaluator
        )

    $assignmentPattern =
        '(?im)(?<prefix>["'']?' +
        '(?:password|pwd|passphrase|secret|token|api[_-]?key|' +
        'client[_-]?secret|account[_-]?key|access[_-]?key|' +
        'connectionstring)' +
        '["'']?\s*(?:=|:)\s*["'']?)' +
        '(?<value>[^"''\s,;)}\]>]{4,})'

    $assignmentRegex =
        New-Object `
            -TypeName System.Text.RegularExpressions.Regex `
            -ArgumentList $assignmentPattern

    $assignmentEvaluator =
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)

            if ($documentationOrTest)
            {
                return $match.Value
            }

            $prefix = $match.Groups['prefix'].Value
            $value = $match.Groups['value'].Value
            $wasQuoted =
                $prefix.EndsWith('"') -or
                $prefix.EndsWith("'")

            if (
                Test-IsPlaceholderValue `
                    -Value $value `
                    -WasQuoted:$wasQuoted
            )
            {
                return $match.Value
            }

            $lineNumber =
                Get-LineNumberAtIndex `
                    -Text $sanitizedText `
                    -Index $match.Index

            Add-SecurityFinding `
                -RuleId 'SEC-SENSITIVE-ASSIGNMENT' `
                -RelativePath $RelativePath `
                -LineNumber $lineNumber `
                -Source $FindingSource

            $state.Count++
            return (
                Replace-MatchGroup `
                    -Match $match `
                    -Group $match.Groups['value'] `
                    -Replacement '[REDACTED:SENSITIVE_VALUE]'
            )
        }

    $sanitizedText =
        $assignmentRegex.Replace(
            $sanitizedText,
            $assignmentEvaluator
        )

    $piiPattern =
        '(?im)(?<prefix>["'']?' +
        '(?:customer[_-]?(?:email|phone|account)|' +
        'email|phone|account[_-]?number|bank[_-]?account)' +
        '["'']?\s*(?:=|:)\s*["'']?)' +
        '(?<value>[^"''\s,;}\]]{5,})'

    $piiRegex =
        New-Object `
            -TypeName System.Text.RegularExpressions.Regex `
            -ArgumentList $piiPattern

    $piiEvaluator =
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)

            if ($documentationOrTest)
            {
                return $match.Value
            }

            $value = $match.Groups['value'].Value
            $looksLikeActualValue =
                $value.Contains('@') -or
                $value -match '(?<!\d)\d{8,}(?!\d)'

            if (
                -not $looksLikeActualValue -or
                (Test-IsPlaceholderValue -Value $value -WasQuoted)
            )
            {
                return $match.Value
            }

            $lineNumber =
                Get-LineNumberAtIndex `
                    -Text $sanitizedText `
                    -Index $match.Index

            Add-SecurityFinding `
                -RuleId 'SEC-POTENTIAL-EMBEDDED-PII' `
                -RelativePath $RelativePath `
                -LineNumber $lineNumber `
                -Source $FindingSource

            $state.Count++
            return (
                Replace-MatchGroup `
                    -Match $match `
                    -Group $match.Groups['value'] `
                    -Replacement '[REDACTED:POTENTIAL_PII]'
            )
        }

    $sanitizedText =
        $piiRegex.Replace(
            $sanitizedText,
            $piiEvaluator
        )

    return [pscustomobject]@{
        Text = $sanitizedText
        FindingCount = $state.Count
        Sanitized = ($state.Count -gt 0)
    }
}

function Get-PathExclusion
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $normalizedPath = $RelativePath.Replace('\', '/')
    $segments = $normalizedPath.Split('/')
    $prunedDirectoryNames = @(
        '.git',
        '.vs',
        'bin',
        'obj',
        'TestResults',
        'artifacts',
        'log',
        'logs',
        'backup',
        'backups'
    )

    for ($index = 0; $index -lt ($segments.Length - 1); $index++)
    {
        foreach ($prunedName in $prunedDirectoryNames)
        {
            if (
                [string]::Equals(
                    $segments[$index],
                    $prunedName,
                    [System.StringComparison]::OrdinalIgnoreCase
                )
            )
            {
                $directoryPath =
                    [string]::Join(
                        '/',
                        [string[]]$segments[0..$index]
                    )

                return [pscustomobject]@{
                    Excluded = $true
                    Reason = 'PrunedDirectory'
                    PrunedDirectory = $directoryPath
                    SecurityRuleId = ''
                }
            }
        }
    }

    foreach (
        $sensitiveDirectoryName in @(
            'AppData',
            'customer-data',
            'store-data',
            'production-data',
            'runtime-data'
        )
    )
    {
        for ($index = 0; $index -lt ($segments.Length - 1); $index++)
        {
            if (
                [string]::Equals(
                    $segments[$index],
                    $sensitiveDirectoryName,
                    [System.StringComparison]::OrdinalIgnoreCase
                )
            )
            {
                $directoryPath =
                    [string]::Join(
                        '/',
                        [string[]]$segments[0..$index]
                    )

                return [pscustomobject]@{
                    Excluded = $true
                    Reason = 'StoreOrMachineDataDirectory'
                    PrunedDirectory = $directoryPath
                    SecurityRuleId = 'SEC-FORBIDDEN-DATA-PATH'
                }
            }
        }
    }

    $fileName =
        [System.IO.Path]::GetFileName($normalizedPath).ToLowerInvariant()
    $extension =
        [System.IO.Path]::GetExtension($normalizedPath).ToLowerInvariant()
    $isEfMigrationFile =
        $normalizedPath -match
            '(?i)^src/POS\.Infrastructure/Persistence/Migrations/\d{14}_[^/]+\.cs$'

    $databaseExtensions = @('.db', '.db3', '.sqlite', '.sqlite3', '.mdf', '.ldf')
    if (
        $databaseExtensions -contains $extension -or
        $fileName.EndsWith('-wal') -or
        $fileName.EndsWith('-shm') -or
        $fileName.EndsWith('-journal')
    )
    {
        return [pscustomobject]@{
            Excluded = $true
            Reason = 'DatabaseOrDatabaseSidecar'
            PrunedDirectory = ''
            SecurityRuleId = 'SEC-DATABASE-OR-CUSTOMER-DATA-FILE'
        }
    }

    $binaryExtensions = @(
        '.dll', '.exe', '.pdb', '.so', '.dylib', '.class', '.pyc',
        '.o', '.lib', '.a', '.bin',
        '.zip', '.7z', '.rar', '.nupkg', '.tar', '.gz', '.bz2', '.xz',
        '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.ico', '.webp',
        '.pdf', '.doc', '.docx', '.xls', '.xlsx'
    )

    if ($binaryExtensions -contains $extension)
    {
        return [pscustomobject]@{
            Excluded = $true
            Reason = 'KnownBinaryArchiveOrDocumentType'
            PrunedDirectory = ''
            SecurityRuleId = ''
        }
    }

    $credentialExtensions = @(
        '.pfx', '.p12', '.p8', '.key', '.ppk', '.kdbx',
        '.jks', '.keystore', '.snk'
    )

    $privateKeyNames = @(
        'id_rsa',
        'id_dsa',
        'id_ecdsa',
        'id_ed25519'
    )

    $machineCredentialNames = @(
        'credentials.json',
        'secrets.json',
        'appsettings.local.json',
        'appsettings.production.local.json'
    )

    if (
        $credentialExtensions -contains $extension -or
        $privateKeyNames -contains $fileName -or
        $machineCredentialNames -contains $fileName
    )
    {
        return [pscustomobject]@{
            Excluded = $true
            Reason = 'CredentialContainerOrMachineCredentialFile'
            PrunedDirectory = ''
            SecurityRuleId = 'SEC-CREDENTIAL-CONTAINER'
        }
    }

    if (
        $normalizedPath -match
            '(?i)(?:^|/)[^/]*[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}[^/]*$' -or
        (
            -not $isEfMigrationFile -and
            $fileName -match '(?<!\d)\d{10,}(?!\d)'
        )
    )
    {
        return [pscustomobject]@{
            Excluded = $true
            Reason = 'PotentialSensitiveIdentifierInPath'
            PrunedDirectory = ''
            SecurityRuleId = 'SEC-SENSITIVE-PATH'
        }
    }

    return [pscustomobject]@{
        Excluded = $false
        Reason = ''
        PrunedDirectory = ''
        SecurityRuleId = ''
    }
}

function Test-PathHasReparsePoint
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$AbsolutePath
    )

    $currentPath = $AbsolutePath

    while (
        Test-FullPathWithinRoot `
            -RootPath $RepositoryRoot `
            -CandidatePath $currentPath `
            -AllowRoot
    )
    {
        $item = Get-Item -LiteralPath $currentPath
        if (
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
        )
        {
            return $true
        }

        if (
            [string]::Equals(
                (Get-NormalizedFullPath -LiteralPath $currentPath),
                (Get-NormalizedFullPath -LiteralPath $RepositoryRoot),
                [System.StringComparison]::OrdinalIgnoreCase
            )
        )
        {
            break
        }

        $currentPath = [System.IO.Path]::GetDirectoryName($currentPath)
    }

    return $false
}

function Convert-BytesToText
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [byte[]]$Bytes
    )

    if (
        $Bytes.Length -ge 4 -and
        (
            (
                $Bytes[0] -eq 0x00 -and
                $Bytes[1] -eq 0x00 -and
                $Bytes[2] -eq 0xFE -and
                $Bytes[3] -eq 0xFF
            ) -or
            (
                $Bytes[0] -eq 0xFF -and
                $Bytes[1] -eq 0xFE -and
                $Bytes[2] -eq 0x00 -and
                $Bytes[3] -eq 0x00
            )
        )
    )
    {
        throw 'UTF-32 source is not supported by this exporter.'
    }

    $encodingName = 'UTF-8'
    $offset = 0
    $encoding = $script:Utf8Strict

    if (
        $Bytes.Length -ge 3 -and
        $Bytes[0] -eq 0xEF -and
        $Bytes[1] -eq 0xBB -and
        $Bytes[2] -eq 0xBF
    )
    {
        $encodingName = 'UTF-8 with BOM'
        $offset = 3
        $encoding = $script:Utf8Strict
    }
    elseif (
        $Bytes.Length -ge 2 -and
        $Bytes[0] -eq 0xFF -and
        $Bytes[1] -eq 0xFE
    )
    {
        $encodingName = 'UTF-16 LE with BOM'
        $offset = 2
        $encoding = $script:Utf16LittleEndianStrict
    }
    elseif (
        $Bytes.Length -ge 2 -and
        $Bytes[0] -eq 0xFE -and
        $Bytes[1] -eq 0xFF
    )
    {
        $encodingName = 'UTF-16 BE with BOM'
        $offset = 2
        $encoding = $script:Utf16BigEndianStrict
    }

    $text = $encoding.GetString(
        $Bytes,
        $offset,
        $Bytes.Length - $offset
    )

    if ($text.IndexOf([char]0) -ge 0)
    {
        throw 'Decoded source contains a NUL character.'
    }

    return [pscustomobject]@{
        Text = $text
        EncodingName = $encodingName
    }
}

function ConvertTo-MetadataValue
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    return $Value.
        Replace('\', '\\').
        Replace("`r", '\r').
        Replace("`n", '\n')
}

function Get-PackKey
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $normalizedPath = $RelativePath.Replace('\', '/')
    $projectMemoryPaths = @(
        'AGENTS.md',
        'docs/project/ARCHITECTURE.md',
        'docs/project/BUSINESS-INVARIANTS.md',
        'docs/project/CHECKPOINT-WORKFLOW.md',
        'docs/project/CURRENT-STATE.md',
        'docs/project/DECISIONS.md',
        'docs/project/KNOWN-ISSUES.md',
        'docs/project/MASTER-ROADMAP.md',
        'docs/project/TEST-BASELINE.md'
    )

    foreach ($memoryPath in $projectMemoryPaths)
    {
        if (
            [string]::Equals(
                $normalizedPath,
                $memoryPath,
                [System.StringComparison]::OrdinalIgnoreCase
            )
        )
        {
            return 'ProjectMemory'
        }
    }

    if (
        $normalizedPath.StartsWith(
            'src/POS.Domain/',
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        return 'Domain'
    }

    if (
        $normalizedPath.StartsWith(
            'src/POS.Application/',
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        return 'Application'
    }

    if (
        $normalizedPath.StartsWith(
            'src/POS.Infrastructure/',
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        return 'Infrastructure'
    }

    if (
        $normalizedPath.StartsWith(
            'src/POS.Wpf/',
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        return 'Wpf'
    }

    if (
        $normalizedPath.StartsWith(
            'tests/',
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        return 'Tests'
    }

    if (
        $normalizedPath.StartsWith(
            'scripts/',
            [System.StringComparison]::OrdinalIgnoreCase
        ) -or
        $normalizedPath.StartsWith(
            '.github/',
            [System.StringComparison]::OrdinalIgnoreCase
        ) -or
        [string]::Equals(
            $normalizedPath,
            'Jenkinsfile',
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        return 'ScriptsCi'
    }

    if (
        -not $normalizedPath.Contains('/') -or
        $normalizedPath.StartsWith(
            'build/',
            [System.StringComparison]::OrdinalIgnoreCase
        ) -or
        $normalizedPath.StartsWith(
            'eng/',
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        return 'RootBuildConfig'
    }

    return 'Other'
}

function New-SourcePackText
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackTitle,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Entries
    )

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine('POS ENTERPRISE PROJECT CONTEXT SOURCE PACK')
    [void]$builder.AppendLine(('Pack: {0}' -f $PackTitle))
    [void]$builder.AppendLine('Generated artifact encoding: UTF-8 without BOM')
    [void]$builder.AppendLine(('EntryCount: {0}' -f $Entries.Count))
    [void]$builder.AppendLine(
        'Content is complete except for targeted in-memory redaction markers.'
    )
    [void]$builder.AppendLine()

    foreach ($entry in $Entries)
    {
        [void]$builder.AppendLine(
            '================================================================'
        )
        [void]$builder.AppendLine('SOURCE ENTRY')
        [void]$builder.AppendLine(
            'AbsolutePath: {0}' -f (
                ConvertTo-MetadataValue -Value $entry.AbsolutePath
            )
        )
        [void]$builder.AppendLine(
            'RelativePath: {0}' -f (
                ConvertTo-MetadataValue -Value $entry.RelativePath
            )
        )
        [void]$builder.AppendLine(
            'SourceSizeBytes: {0}' -f $entry.SourceSizeBytes
        )
        [void]$builder.AppendLine(
            'ModifiedUtc: {0}' -f $entry.ModifiedUtc
        )
        [void]$builder.AppendLine(
            'SourceSha256: {0}' -f $entry.SourceSha256
        )
        [void]$builder.AppendLine(
            'DetectedSourceEncoding: {0}' -f $entry.SourceEncoding
        )
        [void]$builder.AppendLine(
            'Sanitized: {0}' -f $entry.Sanitized.ToString().ToLowerInvariant()
        )
        [void]$builder.AppendLine(
            'SanitizationFindingCount: {0}' -f
                $entry.SanitizationFindingCount
        )
        [void]$builder.AppendLine(
            'PackedContentCharacterLength: {0}' -f
                $entry.SanitizedContent.Length
        )
        [void]$builder.AppendLine('--- CONTENT BEGIN ---')
        [void]$builder.Append($entry.SanitizedContent)

        if (
            $entry.SanitizedContent.Length -eq 0 -or
            -not $entry.SanitizedContent.EndsWith(
                [System.Environment]::NewLine
            )
        )
        {
            [void]$builder.AppendLine()
        }

        [void]$builder.AppendLine('--- CONTENT END ---')
        [void]$builder.AppendLine()
    }

    return $builder.ToString()
}

function New-UntrackedDiffRepresentation
{
    param(
        [Parameter(Mandatory = $true)]
        [object]$Entry
    )

    $pathBytes = $script:Utf8NoBom.GetBytes($Entry.RelativePath)
    $pathBase64 = [System.Convert]::ToBase64String($pathBytes)
    $builder = New-Object System.Text.StringBuilder

    [void]$builder.AppendLine(
        '# CONTEXT-EXPORTER-UNTRACKED-NEW-FILE-V1 BEGIN'
    )
    [void]$builder.AppendLine(
        '# RelativePathUtf8Base64: {0}' -f $pathBase64
    )
    [void]$builder.AppendLine(
        '# SourceSha256: {0}' -f $Entry.SourceSha256
    )
    [void]$builder.AppendLine(
        '# Sanitized: {0}' -f $Entry.Sanitized.ToString().ToLowerInvariant()
    )
    [void]$builder.AppendLine(
        '# ContentCharacterLength: {0}' -f
            $Entry.SanitizedContent.Length
    )
    [void]$builder.AppendLine('# --- CONTENT BEGIN ---')
    [void]$builder.Append($Entry.SanitizedContent)

    if (
        $Entry.SanitizedContent.Length -eq 0 -or
        -not $Entry.SanitizedContent.EndsWith(
            [System.Environment]::NewLine
        )
    )
    {
        [void]$builder.AppendLine()
    }

    [void]$builder.AppendLine('# --- CONTENT END ---')
    [void]$builder.AppendLine(
        '# CONTEXT-EXPORTER-UNTRACKED-NEW-FILE-V1 END'
    )
    [void]$builder.AppendLine()

    return $builder.ToString()
}

function New-Manifest
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackRoot,

        [Parameter(Mandatory = $true)]
        [string]$ManifestPath
    )

    $artifactFiles =
        Get-ChildItem -LiteralPath $PackRoot -File -Recurse |
        Where-Object {
            -not [string]::Equals(
                $_.FullName,
                $ManifestPath,
                [System.StringComparison]::OrdinalIgnoreCase
            )
        }

    $artifactByRelativePath = @{}
    foreach ($artifactFile in $artifactFiles)
    {
        $relativePath =
            Get-RelativePathFromRoot `
                -RootPath $PackRoot `
                -AbsolutePath $artifactFile.FullName

        if ($artifactByRelativePath.ContainsKey($relativePath))
        {
            Throw-ExportFailure `
                -ExitCode $script:ExitManifestFailure `
                -SafeMessage 'Duplicate artifact path found while creating manifest.'
        }

        $artifactByRelativePath[$relativePath] = $artifactFile.FullName
    }

    [string[]]$relativePaths = @($artifactByRelativePath.Keys)
    [System.Array]::Sort(
        $relativePaths,
        [System.StringComparer]::Ordinal
    )

    $rows = New-Object 'System.Collections.Generic.List[object]'
    foreach ($relativePath in $relativePaths)
    {
        $artifactPath = [string]$artifactByRelativePath[$relativePath]
        $bytes = Read-ArtifactBytes -LiteralPath $artifactPath
        $rows.Add(
            [pscustomobject]@{
                RelativePath = $relativePath
                SizeBytes = $bytes.Length
                Sha256 = Get-Sha256Hex -Bytes $bytes
            }
        )
    }

    $manifestText = ConvertTo-CsvText -Rows $rows.ToArray()
    Write-Utf8Artifact `
        -LiteralPath $ManifestPath `
        -Content $manifestText `
        -AllowReplace
}

function Test-Manifest
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackRoot,

        [Parameter(Mandatory = $true)]
        [string]$ManifestPath
    )

    $errors = New-Object 'System.Collections.Generic.List[string]'
    $manifestRows =
        Import-Csv -LiteralPath $ManifestPath -Encoding UTF8

    $manifestPaths =
        New-Object `
            -TypeName 'System.Collections.Generic.HashSet[string]' `
            -ArgumentList (
                [System.StringComparer]::OrdinalIgnoreCase
            )

    foreach ($row in $manifestRows)
    {
        $relativePath = [string]$row.RelativePath

        if (-not $manifestPaths.Add($relativePath))
        {
            $errors.Add('Duplicate manifest entry.')
            continue
        }

        if (
            [string]::Equals(
                $relativePath,
                'MANIFEST-SHA256.csv',
                [System.StringComparison]::OrdinalIgnoreCase
            )
        )
        {
            $errors.Add('Manifest must not list itself.')
            continue
        }

        try
        {
            $artifactPath =
                [System.IO.Path]::GetFullPath(
                    [System.IO.Path]::Combine(
                        $PackRoot,
                        $relativePath.Replace(
                            '/',
                            [System.IO.Path]::DirectorySeparatorChar
                        )
                    )
                )
        }
        catch
        {
            $errors.Add('Invalid artifact-relative path in manifest.')
            continue
        }

        if (
            -not (
                Test-FullPathWithinRoot `
                    -RootPath $PackRoot `
                    -CandidatePath $artifactPath
            )
        )
        {
            $errors.Add('Manifest entry escapes the pack root.')
            continue
        }

        if (-not [System.IO.File]::Exists($artifactPath))
        {
            $errors.Add('Manifest artifact is missing.')
            continue
        }

        $bytes = Read-ArtifactBytes -LiteralPath $artifactPath
        $actualHash = Get-Sha256Hex -Bytes $bytes
        $expectedSize = 0L

        if (
            -not [long]::TryParse(
                [string]$row.SizeBytes,
                [ref]$expectedSize
            )
        )
        {
            $errors.Add('Manifest contains an invalid size.')
            continue
        }

        if ($expectedSize -ne $bytes.LongLength)
        {
            $errors.Add('Manifest size mismatch.')
        }

        if (
            -not [string]::Equals(
                [string]$row.Sha256,
                $actualHash,
                [System.StringComparison]::OrdinalIgnoreCase
            )
        )
        {
            $errors.Add('Manifest hash mismatch.')
        }
    }

    $actualArtifactFiles =
        Get-ChildItem -LiteralPath $PackRoot -File -Recurse

    foreach ($artifactFile in $actualArtifactFiles)
    {
        $relativePath =
            Get-RelativePathFromRoot `
                -RootPath $PackRoot `
                -AbsolutePath $artifactFile.FullName

        if (
            [string]::Equals(
                $relativePath,
                'MANIFEST-SHA256.csv',
                [System.StringComparison]::OrdinalIgnoreCase
            )
        )
        {
            continue
        }

        if (-not $manifestPaths.Contains($relativePath))
        {
            $errors.Add('Artifact exists outside the manifest.')
        }
    }

    return [pscustomobject]@{
        Passed = ($errors.Count -eq 0)
        ErrorCount = $errors.Count
    }
}

function New-PackDirectory
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputRoot
    )

    if (-not (Test-Path -LiteralPath $OutputRoot))
    {
        [void][System.IO.Directory]::CreateDirectory($OutputRoot)
    }

    for ($attempt = 0; $attempt -lt 1000; $attempt++)
    {
        $timestamp =
            [System.DateTime]::UtcNow.ToString(
                'yyyyMMddTHHmmssfffffffZ',
                [System.Globalization.CultureInfo]::InvariantCulture
            )

        $suffix = ''
        if ($attempt -gt 0)
        {
            $suffix = '-{0:D3}' -f $attempt
        }

        $directoryName =
            'project-context-{0}{1}' -f $timestamp, $suffix
        $candidatePath =
            [System.IO.Path]::Combine(
                $OutputRoot,
                $directoryName
            )

        if (-not (Test-Path -LiteralPath $candidatePath))
        {
            [void](New-Item -ItemType Directory -Path $candidatePath)
            return $candidatePath
        }
    }

    Throw-ExportFailure `
        -ExitCode $script:ExitRepositoryFailure `
        -SafeMessage 'Unable to allocate a unique timestamped pack directory.'
}

function Invoke-ProjectContextExport
{
    $createdUtc = [System.DateTime]::UtcNow

    if ([string]::IsNullOrWhiteSpace($PSScriptRoot))
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'PSScriptRoot is unavailable.'
    }

    $scriptDirectory = Get-NormalizedFullPath -LiteralPath $PSScriptRoot
    $repositoryRoot =
        Get-NormalizedFullPath `
            -LiteralPath (
                [System.IO.Path]::Combine(
                    $scriptDirectory,
                    '..'
                )
            )

    if (-not [System.IO.Directory]::Exists($repositoryRoot))
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'Derived repository root does not exist.'
    }

    $gitRootResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'resolve repository root' `
            -Arguments @('rev-parse', '--show-toplevel')

    $gitRepositoryRoot =
        Get-NormalizedFullPath -LiteralPath $gitRootResult.Text.Trim()

    if (
        -not [string]::Equals(
            $repositoryRoot,
            $gitRepositoryRoot,
            [System.StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'PSScriptRoot-derived root does not match Git root.'
    }

    $outputRoot =
        [System.IO.Path]::Combine(
            $repositoryRoot,
            'artifacts',
            'project-context'
        )

    if (
        -not (
            Test-FullPathWithinRoot `
                -RootPath $repositoryRoot `
                -CandidatePath $outputRoot
        )
    )
    {
        Throw-ExportFailure `
            -ExitCode $script:ExitRepositoryFailure `
            -SafeMessage 'Output root escaped the repository.'
    }

    $gitStateResults =
        New-Object 'System.Collections.Generic.List[object]'

    $branchResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'read branch' `
            -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
    $gitStateResults.Add($branchResult)

    $headResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'read HEAD' `
            -Arguments @('rev-parse', 'HEAD')
    $gitStateResults.Add($headResult)

    $originMainResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'read local origin/main' `
            -Arguments @('rev-parse', 'origin/main')
    $gitStateResults.Add($originMainResult)

    $aheadBehindResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'read ahead and behind' `
            -Arguments @(
                'rev-list',
                '--left-right',
                '--count',
                'origin/main...HEAD'
            )
    $gitStateResults.Add($aheadBehindResult)

    $statusBranchResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'read short branch status' `
            -Arguments @('status', '--short', '--branch')
    $gitStateResults.Add($statusBranchResult)

    $statusPorcelainResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'read porcelain status' `
            -Arguments @('status', '--porcelain=v1', '-uall')
    $gitStateResults.Add($statusPorcelainResult)

    $logResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'read recent log' `
            -Arguments @('log', '-5', '--oneline', '--decorate')
    $gitStateResults.Add($logResult)

    $trackedResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'inventory tracked paths' `
            -Arguments @('ls-files', '--cached', '-z')

    $untrackedResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'inventory untracked non-ignored paths' `
            -Arguments @(
                'ls-files',
                '--others',
                '--exclude-standard',
                '-z'
            )

    $trackedPaths =
        Split-NulTerminatedGitPaths -CapturedText $trackedResult.Text
    $untrackedPaths =
        Split-NulTerminatedGitPaths -CapturedText $untrackedResult.Text

    $candidateByPath = @{}
    $duplicateCount = 0

    foreach ($trackedPath in $trackedPaths)
    {
        $resolved =
            ConvertFrom-GitRelativePath `
                -RepositoryRoot $repositoryRoot `
                -GitPath $trackedPath

        if ($candidateByPath.ContainsKey($resolved.RelativePath))
        {
            $duplicateCount++
            $candidateByPath[$resolved.RelativePath].IsTracked = $true
        }
        else
        {
            $candidateByPath[$resolved.RelativePath] =
                [pscustomobject]@{
                    RelativePath = $resolved.RelativePath
                    AbsolutePath = $resolved.AbsolutePath
                    IsTracked = $true
                    IsUntracked = $false
                    IsExcluded = $false
                    ExclusionReason = ''
                    PackKey = ''
                    PackFile = ''
                    Status = ''
                    FailureReason = ''
                    SourceSizeBytes = ''
                    ModifiedUtc = ''
                    SourceSha256 = ''
                    SourceEncoding = ''
                    Sanitized = $false
                    SanitizationFindingCount = 0
                    SanitizedContent = ''
                }
        }
    }

    foreach ($untrackedPath in $untrackedPaths)
    {
        $resolved =
            ConvertFrom-GitRelativePath `
                -RepositoryRoot $repositoryRoot `
                -GitPath $untrackedPath

        if ($candidateByPath.ContainsKey($resolved.RelativePath))
        {
            $duplicateCount++
            $candidateByPath[$resolved.RelativePath].IsUntracked = $true
        }
        else
        {
            $candidateByPath[$resolved.RelativePath] =
                [pscustomobject]@{
                    RelativePath = $resolved.RelativePath
                    AbsolutePath = $resolved.AbsolutePath
                    IsTracked = $false
                    IsUntracked = $true
                    IsExcluded = $false
                    ExclusionReason = ''
                    PackKey = ''
                    PackFile = ''
                    Status = ''
                    FailureReason = ''
                    SourceSizeBytes = ''
                    ModifiedUtc = ''
                    SourceSha256 = ''
                    SourceEncoding = ''
                    Sanitized = $false
                    SanitizationFindingCount = 0
                    SanitizedContent = ''
                }
        }
    }

    [string[]]$candidatePaths = @($candidateByPath.Keys)
    [System.Array]::Sort(
        $candidatePaths,
        [System.StringComparer]::Ordinal
    )

    $exclusionRows = New-Object 'System.Collections.Generic.List[object]'
    $prunedDirectorySet =
        New-Object `
            -TypeName 'System.Collections.Generic.HashSet[string]' `
            -ArgumentList (
                [System.StringComparer]::OrdinalIgnoreCase
            )

    $eligibleEntries = New-Object 'System.Collections.Generic.List[object]'
    $eligibleCount = 0
    $packedEligibleCount = 0
    $excludedCount = 0
    $missingCount = 0
    $unreadableCount = 0
    $stabilityFailureCount = 0

    $packFileByKey = @{
        RootBuildConfig = 'SOURCE-PACK-01-ROOT-BUILD-CONFIG.txt'
        Domain = 'SOURCE-PACK-02-DOMAIN.txt'
        Application = 'SOURCE-PACK-03-APPLICATION.txt'
        Infrastructure = 'SOURCE-PACK-04-INFRASTRUCTURE.txt'
        Wpf = 'SOURCE-PACK-05-WPF.txt'
        Tests = 'SOURCE-PACK-06-TESTS.txt'
        ScriptsCi = 'SOURCE-PACK-07-SCRIPTS-CI.txt'
        ProjectMemory = 'SOURCE-PACK-08-PROJECT-MEMORY.txt'
        Other = 'SOURCE-PACK-09-OTHER-TEXT.txt'
    }

    foreach ($candidatePath in $candidatePaths)
    {
        $candidate = $candidateByPath[$candidatePath]
        $exclusion =
            Get-PathExclusion -RelativePath $candidate.RelativePath

        if ($exclusion.Excluded)
        {
            $candidate.IsExcluded = $true
            $candidate.ExclusionReason = $exclusion.Reason
            $candidate.Status = 'Excluded'
            $excludedCount++

            $exclusionRows.Add(
                [pscustomobject]@{
                    RelativePath =
                        ConvertTo-SafeReportedPath `
                            -RelativePath $candidate.RelativePath
                    RecordType = 'File'
                    Reason = $exclusion.Reason
                    SecurityRuleId = $exclusion.SecurityRuleId
                }
            )

            if (
                -not [string]::IsNullOrEmpty(
                    $exclusion.PrunedDirectory
                ) -and
                $prunedDirectorySet.Add(
                    $exclusion.PrunedDirectory
                )
            )
            {
                $exclusionRows.Add(
                    [pscustomobject]@{
                        RelativePath =
                            ConvertTo-SafeReportedPath `
                                -RelativePath $exclusion.PrunedDirectory
                        RecordType = 'PrunedDirectory'
                        Reason = $exclusion.Reason
                        SecurityRuleId = $exclusion.SecurityRuleId
                    }
                )
            }

            if (
                -not [string]::IsNullOrEmpty(
                    $exclusion.SecurityRuleId
                )
            )
            {
                Add-SecurityFinding `
                    -RuleId $exclusion.SecurityRuleId `
                    -RelativePath $candidate.RelativePath `
                    -LineNumber $null `
                    -Source 'InventoryPathClassification'
            }

            continue
        }

        $eligibleCount++
        $candidate.PackKey = Get-PackKey -RelativePath $candidate.RelativePath
        $candidate.PackFile = $packFileByKey[$candidate.PackKey]

        if (-not [System.IO.File]::Exists($candidate.AbsolutePath))
        {
            $candidate.Status = 'Missing'
            $candidate.FailureReason = 'MissingAtRead'
            $missingCount++
            $eligibleEntries.Add($candidate)
            continue
        }

        try
        {
            if (
                Test-PathHasReparsePoint `
                    -RepositoryRoot $repositoryRoot `
                    -AbsolutePath $candidate.AbsolutePath
            )
            {
                $candidate.Status = 'Unreadable'
                $candidate.FailureReason = 'UnsafeReparsePoint'
                $unreadableCount++
                $eligibleEntries.Add($candidate)
                continue
            }
        }
        catch
        {
            $candidate.Status = 'Unreadable'
            $candidate.FailureReason = 'MetadataReadFailure'
            $unreadableCount++
            $eligibleEntries.Add($candidate)
            continue
        }

        try
        {
            $metadataBefore =
                Get-Item -LiteralPath $candidate.AbsolutePath

            if ($metadataBefore.PSIsContainer)
            {
                $candidate.Status = 'Unreadable'
                $candidate.FailureReason = 'CandidateIsDirectory'
                $unreadableCount++
                $eligibleEntries.Add($candidate)
                continue
            }

            $sourceBytes =
                [System.IO.File]::ReadAllBytes(
                    $candidate.AbsolutePath
                )

            if (-not [System.IO.File]::Exists($candidate.AbsolutePath))
            {
                $candidate.Status = 'Missing'
                $candidate.FailureReason = 'MissingAfterRead'
                $missingCount++
                $eligibleEntries.Add($candidate)
                continue
            }

            $metadataAfter =
                Get-Item -LiteralPath $candidate.AbsolutePath

            $candidate.SourceSizeBytes = $sourceBytes.LongLength
            $candidate.ModifiedUtc =
                $metadataBefore.LastWriteTimeUtc.ToString(
                    'o',
                    [System.Globalization.CultureInfo]::InvariantCulture
                )
            $candidate.SourceSha256 =
                Get-Sha256Hex -Bytes $sourceBytes

            $isStable =
                $metadataBefore.Length -eq $sourceBytes.LongLength -and
                $metadataAfter.Length -eq $sourceBytes.LongLength -and
                $metadataBefore.LastWriteTimeUtc.Ticks -eq
                    $metadataAfter.LastWriteTimeUtc.Ticks -and
                $metadataBefore.CreationTimeUtc.Ticks -eq
                    $metadataAfter.CreationTimeUtc.Ticks -and
                $metadataBefore.Attributes -eq $metadataAfter.Attributes

            if (-not $isStable)
            {
                $candidate.Status = 'Unreadable'
                $candidate.FailureReason = 'StabilityCheckFailed'
                $unreadableCount++
                $stabilityFailureCount++
                $eligibleEntries.Add($candidate)
                continue
            }

            try
            {
                $decoded = Convert-BytesToText -Bytes $sourceBytes
            }
            catch
            {
                $candidate.Status = 'Unreadable'
                $candidate.FailureReason = 'UnsafeOrUnsupportedTextEncoding'
                $unreadableCount++
                $eligibleEntries.Add($candidate)
                continue
            }

            $protected =
                Protect-TextContent `
                    -RelativePath $candidate.RelativePath `
                    -Text $decoded.Text `
                    -FindingSource 'SourceInventory'

            $candidate.SourceEncoding = $decoded.EncodingName
            $candidate.Sanitized = $protected.Sanitized
            $candidate.SanitizationFindingCount =
                $protected.FindingCount
            $candidate.SanitizedContent = $protected.Text
            $candidate.Status = 'Packed'
            $packedEligibleCount++
            $eligibleEntries.Add($candidate)
        }
        catch
        {
            if (-not [System.IO.File]::Exists($candidate.AbsolutePath))
            {
                $candidate.Status = 'Missing'
                $candidate.FailureReason = 'MissingDuringRead'
                $missingCount++
            }
            else
            {
                $candidate.Status = 'Unreadable'
                $candidate.FailureReason = 'ReadOrMetadataFailure'
                $unreadableCount++
            }

            $eligibleEntries.Add($candidate)
        }
    }

    $packDirectory = New-PackDirectory -OutputRoot $outputRoot

    $packOrder = @(
        [pscustomobject]@{
            Key = 'RootBuildConfig'
            Title = 'Root, build, and configuration'
        },
        [pscustomobject]@{ Key = 'Domain'; Title = 'POS.Domain' },
        [pscustomobject]@{ Key = 'Application'; Title = 'POS.Application' },
        [pscustomobject]@{
            Key = 'Infrastructure'
            Title = 'POS.Infrastructure'
        },
        [pscustomobject]@{ Key = 'Wpf'; Title = 'POS.Wpf' },
        [pscustomobject]@{ Key = 'Tests'; Title = 'Tests' },
        [pscustomobject]@{ Key = 'ScriptsCi'; Title = 'Scripts and CI' },
        [pscustomobject]@{ Key = 'ProjectMemory'; Title = 'Project Memory' },
        [pscustomobject]@{ Key = 'Other'; Title = 'Other eligible text' }
    )

    $packedSeen =
        New-Object `
            -TypeName 'System.Collections.Generic.HashSet[string]' `
            -ArgumentList (
                [System.StringComparer]::OrdinalIgnoreCase
            )

    foreach ($packDefinition in $packOrder)
    {
        $packEntries = New-Object 'System.Collections.Generic.List[object]'

        foreach ($entry in $eligibleEntries)
        {
            if (
                $entry.Status -eq 'Packed' -and
                [string]::Equals(
                    $entry.PackKey,
                    $packDefinition.Key,
                    [System.StringComparison]::Ordinal
                )
            )
            {
                if (-not $packedSeen.Add($entry.RelativePath))
                {
                    $duplicateCount++
                }

                $packEntries.Add($entry)
            }
        }

        $packText =
            New-SourcePackText `
                -PackTitle $packDefinition.Title `
                -Entries $packEntries.ToArray()

        $packPath =
            [System.IO.Path]::Combine(
                $packDirectory,
                $packFileByKey[$packDefinition.Key]
            )

        Write-Utf8Artifact `
            -LiteralPath $packPath `
            -Content $packText
    }

    $sourceIndexRows = New-Object 'System.Collections.Generic.List[object]'
    foreach ($entry in $eligibleEntries)
    {
        $sourceIndexRows.Add(
            [pscustomobject]@{
                RelativePath = $entry.RelativePath
                AbsolutePath = $entry.AbsolutePath
                SourceSizeBytes = $entry.SourceSizeBytes
                ModifiedUtc = $entry.ModifiedUtc
                SourceSha256 = $entry.SourceSha256
                PackFile =
                    if ($entry.Status -eq 'Packed')
                    {
                        $entry.PackFile
                    }
                    else
                    {
                        ''
                    }
                Sanitized =
                    $entry.Sanitized.ToString().ToLowerInvariant()
                SanitizationFindingCount =
                    $entry.SanitizationFindingCount
                Status = $entry.Status
                SourceEncoding = $entry.SourceEncoding
                FailureReason = $entry.FailureReason
            }
        )
    }

    $sourceIndexPath =
        [System.IO.Path]::Combine(
            $packDirectory,
            'SOURCE-INDEX.csv'
        )

    Write-Utf8Artifact `
        -LiteralPath $sourceIndexPath `
        -Content (
            ConvertTo-CsvText -Rows $sourceIndexRows.ToArray()
        )

    $exclusionPath =
        [System.IO.Path]::Combine(
            $packDirectory,
            'EXCLUSIONS.csv'
        )

    if ($exclusionRows.Count -eq 0)
    {
        $exclusionRows.Add(
            [pscustomobject]@{
                RelativePath = ''
                RecordType = 'Summary'
                Reason = 'NoExcludedCandidates'
                SecurityRuleId = ''
            }
        )
    }

    Write-Utf8Artifact `
        -LiteralPath $exclusionPath `
        -Content (
            ConvertTo-CsvText -Rows $exclusionRows.ToArray()
        )

    $gitStateBuilder = New-Object System.Text.StringBuilder
    [void]$gitStateBuilder.AppendLine('POS ENTERPRISE GIT STATE')
    [void]$gitStateBuilder.AppendLine(
        'CapturedUtc={0}' -f $createdUtc.ToString(
            'o',
            [System.Globalization.CultureInfo]::InvariantCulture
        )
    )
    [void]$gitStateBuilder.AppendLine(
        'Branch={0}' -f $branchResult.Text.Trim()
    )
    [void]$gitStateBuilder.AppendLine(
        'HEAD={0}' -f $headResult.Text.Trim()
    )
    [void]$gitStateBuilder.AppendLine(
        'LocalOriginMain={0}' -f $originMainResult.Text.Trim()
    )
    [void]$gitStateBuilder.AppendLine(
        'AheadBehind={0}' -f $aheadBehindResult.Text.Trim()
    )
    [void]$gitStateBuilder.AppendLine()

    foreach ($gitStateResult in $gitStateResults)
    {
        [void]$gitStateBuilder.AppendLine(
            '--- {0} ---' -f $gitStateResult.Operation
        )
        [void]$gitStateBuilder.AppendLine(
            'ExitCode={0}' -f $gitStateResult.ExitCode
        )
        [void]$gitStateBuilder.AppendLine($gitStateResult.Text)
        [void]$gitStateBuilder.AppendLine()
    }

    [void]$gitStateBuilder.AppendLine(
        '--- inventory tracked paths ---'
    )
    [void]$gitStateBuilder.AppendLine(
        'ExitCode={0}' -f $trackedResult.ExitCode
    )
    [void]$gitStateBuilder.AppendLine(
        'PathCount={0}' -f $trackedPaths.Count
    )
    [void]$gitStateBuilder.AppendLine(
        '--- inventory untracked non-ignored paths ---'
    )
    [void]$gitStateBuilder.AppendLine(
        'ExitCode={0}' -f $untrackedResult.ExitCode
    )
    [void]$gitStateBuilder.AppendLine(
        'PathCount={0}' -f $untrackedPaths.Count
    )

    $protectedGitState =
        Protect-TextContent `
            -RelativePath 'GIT-STATE.txt' `
            -Text $gitStateBuilder.ToString() `
            -FindingSource 'GitState'

    $gitStatePath =
        [System.IO.Path]::Combine(
            $packDirectory,
            'GIT-STATE.txt'
        )

    Write-Utf8Artifact `
        -LiteralPath $gitStatePath `
        -Content $protectedGitState.Text

    $diffBuilder = New-Object System.Text.StringBuilder
    [void]$diffBuilder.AppendLine(
        '# POS Enterprise current diff relative to HEAD'
    )
    [void]$diffBuilder.AppendLine(
        '# Tracked sections are Git unified diff output.'
    )
    [void]$diffBuilder.AppendLine(
        '# Eligible untracked sections use ' +
        'CONTEXT-EXPORTER-UNTRACKED-NEW-FILE-V1.'
    )
    [void]$diffBuilder.AppendLine()

    $changedTrackedResult =
        Invoke-GitCapture `
            -RepositoryRoot $repositoryRoot `
            -Operation 'inventory tracked paths changed from HEAD' `
            -Arguments @(
                'diff',
                '--name-only',
                '-z',
                '--no-renames',
                'HEAD',
                '--'
            )

    $changedTrackedPaths =
        Split-NulTerminatedGitPaths `
            -CapturedText $changedTrackedResult.Text

    foreach ($changedTrackedPath in $changedTrackedPaths)
    {
        $resolvedChangedPath =
            ConvertFrom-GitRelativePath `
                -RepositoryRoot $repositoryRoot `
                -GitPath $changedTrackedPath

        $changedExclusion =
            Get-PathExclusion `
                -RelativePath $resolvedChangedPath.RelativePath

        if ($changedExclusion.Excluded)
        {
            continue
        }

        if (
            $candidateByPath.ContainsKey(
                $resolvedChangedPath.RelativePath
            )
        )
        {
            $changedCandidate =
                $candidateByPath[$resolvedChangedPath.RelativePath]

            if (
                $changedCandidate.IsExcluded -or
                $changedCandidate.Status -eq 'Unreadable'
            )
            {
                continue
            }
        }

        $singleDiffResult =
            Invoke-GitCapture `
                -RepositoryRoot $repositoryRoot `
                -Operation 'capture tracked diff in memory' `
                -Arguments @(
                    'diff',
                    '--no-ext-diff',
                    '--no-textconv',
                    '--no-renames',
                    '--full-index',
                    'HEAD',
                    '--',
                    $resolvedChangedPath.RelativePath
                )

        if ($singleDiffResult.Text.Length -gt 0)
        {
            $protectedDiff =
                Protect-TextContent `
                    -RelativePath $resolvedChangedPath.RelativePath `
                    -Text $singleDiffResult.Text `
                    -FindingSource 'TrackedCurrentDiff'

            [void]$diffBuilder.AppendLine($protectedDiff.Text)
            [void]$diffBuilder.AppendLine()
        }
    }

    foreach ($entry in $eligibleEntries)
    {
        if ($entry.IsUntracked -and $entry.Status -eq 'Packed')
        {
            [void]$diffBuilder.Append(
                (New-UntrackedDiffRepresentation -Entry $entry)
            )
        }
    }

    $currentDiffPath =
        [System.IO.Path]::Combine(
            $packDirectory,
            'CURRENT-DIFF-SANITIZED.patch'
        )

    Write-Utf8Artifact `
        -LiteralPath $currentDiffPath `
        -Content $diffBuilder.ToString()

    $securityRows = New-Object 'System.Collections.Generic.List[object]'
    foreach ($finding in $script:SecurityFindings)
    {
        $securityRows.Add($finding)
    }

    if ($securityRows.Count -eq 0)
    {
        $securityRows.Add(
            [pscustomobject]@{
                RuleId = ''
                RelativePath = ''
                LineNumber = ''
                Source = 'NoSecurityFindings'
            }
        )
    }

    $securityFindingsPath =
        [System.IO.Path]::Combine(
            $packDirectory,
            'SECURITY-FINDINGS.csv'
        )

    Write-Utf8Artifact `
        -LiteralPath $securityFindingsPath `
        -Content (
            ConvertTo-CsvText -Rows $securityRows.ToArray()
        )

    $coveragePercent = 0.0
    if ($eligibleCount -gt 0)
    {
        $coveragePercent =
            (
                [double]$packedEligibleCount /
                [double]$eligibleCount
            ) * 100.0
    }

    $packMembershipFailureCount =
        [Math]::Max(
            0,
            $packedEligibleCount - $packedSeen.Count
        )

    if ($packMembershipFailureCount -gt 0)
    {
        $duplicateCount += $packMembershipFailureCount
    }

    $securityFindingCount = $script:SecurityFindings.Count
    $coverageFailure =
        $eligibleCount -le 0 -or
        $packedEligibleCount -ne $eligibleCount -or
        [Math]::Abs($coveragePercent - 100.0) -gt 0.0000001 -or
        $missingCount -ne 0 -or
        $unreadableCount -ne 0 -or
        $duplicateCount -ne 0 -or
        $packedSeen.Count -ne $packedEligibleCount

    $preManifestExitCode = $script:ExitSuccess
    $preManifestResult = 'Success'

    if ($coverageFailure)
    {
        $preManifestExitCode = $script:ExitInventoryFailure
        $preManifestResult = 'InventoryOrCoverageFailure'
    }

    if ($securityFindingCount -gt 0)
    {
        $preManifestExitCode = $script:ExitSecurityFinding
        $preManifestResult = 'ForbiddenDataOrSecurityFinding'
    }

    $artifactNames = New-Object 'System.Collections.Generic.List[string]'
    $artifactNames.Add('README-FIRST.md')
    $artifactNames.Add('GIT-STATE.txt')
    $artifactNames.Add('CURRENT-DIFF-SANITIZED.patch')
    $artifactNames.Add('SOURCE-INDEX.csv')
    $artifactNames.Add('EXCLUSIONS.csv')
    $artifactNames.Add('SECURITY-FINDINGS.csv')
    $artifactNames.Add('COVERAGE-VERIFICATION.md')
    foreach ($packDefinition in $packOrder)
    {
        $artifactNames.Add($packFileByKey[$packDefinition.Key])
    }
    $artifactNames.Add('MANIFEST-SHA256.csv')

    $readmeBuilder = New-Object System.Text.StringBuilder
    [void]$readmeBuilder.AppendLine('# README FIRST — POS Enterprise Context Pack')
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine(
        'Purpose: a timestamped, reviewable snapshot of eligible repository ' +
        'text source and Git state for context transfer.'
    )
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine(
        '- Created UTC: `{0}`' -f $createdUtc.ToString(
            'o',
            [System.Globalization.CultureInfo]::InvariantCulture
        )
    )
    [void]$readmeBuilder.AppendLine(
        '- Repository root: `{0}`' -f $repositoryRoot
    )
    [void]$readmeBuilder.AppendLine(
        '- Branch: `{0}`' -f $branchResult.Text.Trim()
    )
    [void]$readmeBuilder.AppendLine(
        '- Commit: `{0}`' -f $headResult.Text.Trim()
    )
    [void]$readmeBuilder.AppendLine(
        '- Generated text encoding: UTF-8 without BOM.'
    )
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine('## Artifacts')
    [void]$readmeBuilder.AppendLine()
    foreach ($artifactName in $artifactNames)
    {
        [void]$readmeBuilder.AppendLine('- `{0}`' -f $artifactName)
    }
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine('## Security and sanitization')
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine(
        'Known database, database sidecar, binary, archive and credential ' +
        'container types are classified by path/type and are never opened.'
    )
    [void]$readmeBuilder.AppendLine(
        'Eligible text is read once into memory, checked for stability, and ' +
        'targeted sensitive values are redacted only in the pack copy.'
    )
    [void]$readmeBuilder.AppendLine(
        'Identifiers such as Password, PasswordHash, WifiPassword, Token, ' +
        'Secret and ApiKey are not findings by themselves. Documentation, ' +
        'placeholders and clearly synthetic test values are not treated as ' +
        'credentials merely because those identifiers occur.'
    )
    [void]$readmeBuilder.AppendLine(
        'A security finding is recorded without the raw value and forces ' +
        'security exit code 4 unless a higher-precedence failure occurs.'
    )
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine('## Current diff representation')
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine(
        'Tracked changes are sanitized Git unified diffs captured in memory. ' +
        'Eligible untracked files use a complete length-delimited ' +
        'CONTEXT-EXPORTER-UNTRACKED-NEW-FILE-V1 block. Its path is UTF-8 ' +
        'Base64 encoded so spaces, Unicode and unusual path characters are ' +
        'unambiguous; its full sanitized content represents a new file.'
    )
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine('## Coverage summary')
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine(
        '- Inventory candidates: {0}' -f $candidatePaths.Count
    )
    [void]$readmeBuilder.AppendLine(
        '- Eligible: {0}' -f $eligibleCount
    )
    [void]$readmeBuilder.AppendLine(
        '- Packed eligible: {0}' -f $packedEligibleCount
    )
    [void]$readmeBuilder.AppendLine(
        '- Excluded: {0}' -f $excludedCount
    )
    [void]$readmeBuilder.AppendLine(
        '- Missing: {0}' -f $missingCount
    )
    [void]$readmeBuilder.AppendLine(
        '- Unreadable: {0}' -f $unreadableCount
    )
    [void]$readmeBuilder.AppendLine(
        '- Duplicate: {0}' -f $duplicateCount
    )
    [void]$readmeBuilder.AppendLine(
        '- Security findings: {0}' -f $securityFindingCount
    )
    [void]$readmeBuilder.AppendLine(
        '- Coverage percent: {0:F6}' -f $coveragePercent
    )
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine('## Exit codes')
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine('- `0` Success.')
    [void]$readmeBuilder.AppendLine(
        '- `2` Repository, path, precondition or Git failure.'
    )
    [void]$readmeBuilder.AppendLine(
        '- `3` Inventory, missing, unreadable, stability or coverage failure.'
    )
    [void]$readmeBuilder.AppendLine(
        '- `4` Forbidden-data/security finding.'
    )
    [void]$readmeBuilder.AppendLine(
        '- `5` Manifest/hash verification failure.'
    )
    [void]$readmeBuilder.AppendLine('- `10` Unexpected fatal error.')
    [void]$readmeBuilder.AppendLine()
    [void]$readmeBuilder.AppendLine(
        'Precedence: 10 > 2 > 5 > 4 > 3 > 0. A pack must not be interpreted ' +
        'as successful when the exporter process exits non-zero.'
    )
    $preManifestReadmeLine =
        (
            'Pre-manifest result: `{0}`; planned exit code: `{1}`. ' +
            'Final manifest verification can replace this with exit code 5.'
        ) -f $preManifestResult, $preManifestExitCode

    [void]$readmeBuilder.AppendLine(
        $preManifestReadmeLine
    )

    $readmePath =
        [System.IO.Path]::Combine(
            $packDirectory,
            'README-FIRST.md'
        )

    Write-Utf8Artifact `
        -LiteralPath $readmePath `
        -Content $readmeBuilder.ToString()

    $coveragePath =
        [System.IO.Path]::Combine(
            $packDirectory,
            'COVERAGE-VERIFICATION.md'
        )

    $coverageBuilder = New-Object System.Text.StringBuilder
    [void]$coverageBuilder.AppendLine(
        '# COVERAGE VERIFICATION — POS Enterprise Context Pack'
    )
    [void]$coverageBuilder.AppendLine()
    [void]$coverageBuilder.AppendLine(
        '- InventoryCandidateCount: {0}' -f $candidatePaths.Count
    )
    [void]$coverageBuilder.AppendLine(
        '- EligibleCount: {0}' -f $eligibleCount
    )
    [void]$coverageBuilder.AppendLine(
        '- PackedEligibleCount: {0}' -f $packedEligibleCount
    )
    [void]$coverageBuilder.AppendLine(
        '- ExcludedCount: {0}' -f $excludedCount
    )
    [void]$coverageBuilder.AppendLine(
        '- MissingCount: {0}' -f $missingCount
    )
    [void]$coverageBuilder.AppendLine(
        '- UnreadableCount: {0}' -f $unreadableCount
    )
    [void]$coverageBuilder.AppendLine(
        '- StabilityFailureCount: {0}' -f $stabilityFailureCount
    )
    [void]$coverageBuilder.AppendLine(
        '- DuplicateCount: {0}' -f $duplicateCount
    )
    [void]$coverageBuilder.AppendLine(
        '- SecurityFindingCount: {0}' -f $securityFindingCount
    )
    [void]$coverageBuilder.AppendLine(
        '- CoveragePercent: {0:F6}' -f $coveragePercent
    )
    [void]$coverageBuilder.AppendLine(
        '- EachEligiblePackedExactlyOnce: {0}' -f (
            (
                $packedSeen.Count -eq $packedEligibleCount -and
                $packedEligibleCount -eq $eligibleCount
            ).ToString().ToUpperInvariant()
        )
    )
    [void]$coverageBuilder.AppendLine(
        '- ManifestVerificationResult: PENDING'
    )
    [void]$coverageBuilder.AppendLine(
        '- Result: {0}' -f $preManifestResult
    )
    [void]$coverageBuilder.AppendLine(
        '- ExitCode: {0}' -f $preManifestExitCode
    )
    [void]$coverageBuilder.AppendLine()
    [void]$coverageBuilder.AppendLine(
        'CoveragePercent = PackedEligibleCount / EligibleCount * 100. ' +
        'ExcludedCount is not part of EligibleCount.'
    )

    Write-Utf8Artifact `
        -LiteralPath $coveragePath `
        -Content $coverageBuilder.ToString()

    $manifestPath =
        [System.IO.Path]::Combine(
            $packDirectory,
            'MANIFEST-SHA256.csv'
        )

    New-Manifest `
        -PackRoot $packDirectory `
        -ManifestPath $manifestPath

    $firstManifestVerification =
        Test-Manifest `
            -PackRoot $packDirectory `
            -ManifestPath $manifestPath

    $manifestResultText =
        if ($firstManifestVerification.Passed)
        {
            'PASS'
        }
        else
        {
            'FAIL'
        }

    $finalPlannedExitCode = $preManifestExitCode
    $finalPlannedResult = $preManifestResult

    if (-not $firstManifestVerification.Passed)
    {
        $finalPlannedExitCode = $script:ExitManifestFailure
        $finalPlannedResult = 'ManifestOrHashFailure'
    }

    $finalCoverageText =
        $coverageBuilder.ToString().
        Replace(
            'ManifestVerificationResult: PENDING',
            'ManifestVerificationResult: {0}' -f $manifestResultText
        ).
        Replace(
            'Result: {0}' -f $preManifestResult,
            'Result: {0}' -f $finalPlannedResult
        ).
        Replace(
            'ExitCode: {0}' -f $preManifestExitCode,
            'ExitCode: {0}' -f $finalPlannedExitCode
        )

    Write-Utf8Artifact `
        -LiteralPath $coveragePath `
        -Content $finalCoverageText `
        -AllowReplace

    New-Manifest `
        -PackRoot $packDirectory `
        -ManifestPath $manifestPath

    $finalManifestVerification =
        Test-Manifest `
            -PackRoot $packDirectory `
            -ManifestPath $manifestPath

    if (-not $finalManifestVerification.Passed)
    {
        Write-Host (
            'Context Pack created but final manifest verification failed. ' +
            'No artifact was modified after verification.'
        )
        Write-Host ('PackDirectory={0}' -f $packDirectory)
        Write-Host (
            'ManifestVerificationErrorCount={0}' -f
                $finalManifestVerification.ErrorCount
        )
        return $script:ExitManifestFailure
    }

    Write-Host ('PackDirectory={0}' -f $packDirectory)
    Write-Host 'FinalManifestVerification=PASS'
    Write-Host ('CoveragePercent={0:F6}' -f $coveragePercent)
    Write-Host ('SecurityFindingCount={0}' -f $securityFindingCount)

    if ($finalPlannedExitCode -eq $script:ExitSuccess)
    {
        Write-Host 'PROJECT_CONTEXT_EXPORT_RESULT=SUCCESS'
    }
    else
    {
        Write-Host (
            'PROJECT_CONTEXT_EXPORT_RESULT=NON_SUCCESS; ExitCode={0}' -f
                $finalPlannedExitCode
        )
    }

    return $finalPlannedExitCode
}

$processExitCode = $script:ExitUnexpectedFailure

try
{
    $processExitCode = Invoke-ProjectContextExport
}
catch
{
    $exception = $_.Exception
    $processExitCode = $script:ExitUnexpectedFailure

    if (
        $null -ne $exception -and
        $exception.Data.Contains('ProjectContextExitCode')
    )
    {
        $processExitCode =
            [int]$exception.Data['ProjectContextExitCode']
    }

    $safeMessage = 'Unexpected exporter failure.'
    if (
        $processExitCode -ne $script:ExitUnexpectedFailure -and
        $null -ne $exception -and
        -not [string]::IsNullOrWhiteSpace($exception.Message)
    )
    {
        $safeMessage = $exception.Message
    }

    $safeErrorText =
        (
            'Project Context export stopped safely. ExitCode={0}. {1} ' +
            'No captured source value is included in this message.'
        ) -f $processExitCode, $safeMessage

    Write-Error (
        $safeErrorText
    )
}

exit $processExitCode
