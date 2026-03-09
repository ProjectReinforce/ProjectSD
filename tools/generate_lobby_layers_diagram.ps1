Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $root "docs/lobby-scripts_-layer-diagram.png"

$width = 2200
$height = 1450

$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$graphics.Clear([System.Drawing.Color]::FromArgb(250, 250, 248))

$titleFont = New-Object System.Drawing.Font("Segoe UI", 24, [System.Drawing.FontStyle]::Bold)
$groupFont = New-Object System.Drawing.Font("Segoe UI", 16, [System.Drawing.FontStyle]::Bold)
$textFont = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Regular)
$smallFont = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Regular)

$darkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(34, 40, 49))
$mutedBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(96, 103, 112))
$linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(96, 103, 112), 3)
$linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::ArrowAnchor

function New-Style($fill, $header, $border) {
    return @{
        Fill = New-Object System.Drawing.SolidBrush($fill)
        Header = New-Object System.Drawing.SolidBrush($header)
        Border = New-Object System.Drawing.Pen($border, 2)
    }
}

$styles = @{
    Shared = (New-Style ([System.Drawing.Color]::FromArgb(237, 244, 252)) ([System.Drawing.Color]::FromArgb(214, 230, 245)) ([System.Drawing.Color]::FromArgb(96, 145, 191)))
    Bootstrap = (New-Style ([System.Drawing.Color]::FromArgb(244, 239, 229)) ([System.Drawing.Color]::FromArgb(232, 220, 192)) ([System.Drawing.Color]::FromArgb(167, 132, 63)))
    Presentation = (New-Style ([System.Drawing.Color]::FromArgb(232, 245, 237)) ([System.Drawing.Color]::FromArgb(206, 232, 215)) ([System.Drawing.Color]::FromArgb(83, 146, 97)))
    Application = (New-Style ([System.Drawing.Color]::FromArgb(255, 245, 229)) ([System.Drawing.Color]::FromArgb(249, 228, 194)) ([System.Drawing.Color]::FromArgb(201, 139, 35)))
    Infrastructure = (New-Style ([System.Drawing.Color]::FromArgb(246, 236, 247)) ([System.Drawing.Color]::FromArgb(231, 210, 233)) ([System.Drawing.Color]::FromArgb(151, 99, 156)))
    Domain = (New-Style ([System.Drawing.Color]::FromArgb(252, 235, 236)) ([System.Drawing.Color]::FromArgb(244, 208, 211)) ([System.Drawing.Color]::FromArgb(195, 99, 112)))
}

function Draw-Group {
    param(
        [string]$Key,
        [string]$Title,
        [string[]]$Lines,
        [int]$X,
        [int]$Y,
        [int]$Width,
        [int]$Height
    )

    $style = $styles[$Key]
    $rect = New-Object System.Drawing.Rectangle $X, $Y, $Width, $Height
    $headerRect = New-Object System.Drawing.Rectangle $X, $Y, $Width, 48

    $graphics.FillRectangle($style.Fill, $rect)
    $graphics.FillRectangle($style.Header, $headerRect)
    $graphics.DrawRectangle($style.Border, $rect)

    $graphics.DrawString($Title, $groupFont, $darkBrush, ($X + 16), ($Y + 10))

    $textY = $Y + 66
    foreach ($line in $Lines) {
        $graphics.DrawString($line, $textFont, $darkBrush, ($X + 18), $textY)
        $textY += 30
    }

    return @{
        Rect = $rect
        Left = @{ X = $X; Y = $Y + [int]($Height / 2) }
        Right = @{ X = $X + $Width; Y = $Y + [int]($Height / 2) }
        Top = @{ X = $X + [int]($Width / 2); Y = $Y }
        Bottom = @{ X = $X + [int]($Width / 2); Y = $Y + $Height }
    }
}

function Draw-Arrow {
    param(
        [int]$X1,
        [int]$Y1,
        [int]$X2,
        [int]$Y2,
        [string]$Label = ""
    )

    $graphics.DrawLine($linePen, $X1, $Y1, $X2, $Y2)

    if ($Label -ne "") {
        $midX = [int](($X1 + $X2) / 2)
        $midY = [int](($Y1 + $Y2) / 2)
        $size = $graphics.MeasureString($Label, $smallFont)
        $labelRect = New-Object System.Drawing.RectangleF ($midX - ($size.Width / 2) - 6), ($midY - ($size.Height / 2) - 2), ($size.Width + 12), ($size.Height + 4)
        $graphics.FillRectangle([System.Drawing.Brushes]::White, $labelRect)
        $graphics.DrawString($Label, $smallFont, $mutedBrush, $labelRect.X + 6, $labelRect.Y + 2)
    }
}

$graphics.DrawString("Assets/Scripts_ Layer Diagram", $titleFont, $darkBrush, 48, 28)
$graphics.DrawString("Simplified 1:N view centered on the Lobby feature", $textFont, $mutedBrush, 50, 72)

$shared = Draw-Group "Shared" "Shared" @(
    "1:1 SceneContext -> EventBus",
    "1:1 UiShellView -> UiStack",
    "Utility: PendingCallbackTracker",
    "Kernel: Entity, EntityId, Result, ValueObject"
) 50 140 490 240

$bootstrap = Draw-Group "Bootstrap" "Lobby / Bootstrap" @(
    "1:1 LobbyBootstrap -> LobbyView",
    "1:1 LobbyBootstrap -> LobbyInputHandler",
    "1:1 LobbyBootstrap -> LobbyPhotonAdapter",
    "1:N LobbyBootstrap -> UseCases",
    "1:1 LobbyBootstrap -> Repository / Clock / SceneContext"
) 640 140 520 270

$presentation = Draw-Group "Presentation" "Lobby / Presentation" @(
    "1:1 LobbyView -> LobbyInputHandler",
    "1:1 LobbyView -> RoomListView",
    "1:1 LobbyView -> RoomDetailView",
    "1:N LobbyInputHandler -> UseCases"
) 50 500 490 220

$application = Draw-Group "Application" "Lobby / Application" @(
    "UseCases: Create / Join / Leave",
    "UseCases: ChangeTeam / SetReady / StartGame",
    "1:N UseCase -> Ports",
    "1:N UseCase -> Events",
    "N:1 UseCase -> Domain"
) 640 480 520 260

$infrastructure = Draw-Group "Infrastructure" "Lobby / Infrastructure" @(
    "1:1 LobbyRepository -> ILobbyRepository",
    "1:1 ClockAdapter -> IClockPort",
    "1:1 LobbyPhotonAdapter -> ILobbyNetworkPort",
    "1:1 LobbyPhotonAdapter -> EventHandler",
    "1:1 LobbyPhotonAdapter -> PropertyManager / PendingTracker"
) 1280 170 560 270

$domain = Draw-Group "Domain" "Lobby / Domain" @(
    "1:N Lobby -> Room",
    "1:N Room -> RoomMember",
    "N:1 LobbyRule -> Lobby",
    "N:1 LobbyRule -> Room",
    "N:1 RoomMember -> TeamType"
) 1280 540 560 250

Draw-Arrow $shared.Right.X ($shared.Right.Y - 30) $bootstrap.Left.X ($bootstrap.Left.Y - 30) "shared context"
Draw-Arrow $bootstrap.Bottom.X $bootstrap.Bottom.Y $application.Top.X $application.Top.Y "creates"
Draw-Arrow $presentation.Right.X $presentation.Right.Y $application.Left.X $application.Left.Y "calls"
Draw-Arrow $application.Right.X ($application.Right.Y - 40) $infrastructure.Left.X ($infrastructure.Left.Y + 20) "uses ports"
Draw-Arrow $infrastructure.Bottom.X ($infrastructure.Bottom.Y - 20) $domain.Top.X $domain.Top.Y "reads/updates"
Draw-Arrow $application.Right.X ($application.Right.Y + 40) $domain.Left.X ($domain.Left.Y - 10) "coordinates"
Draw-Arrow $bootstrap.Right.X ($bootstrap.Right.Y + 20) $infrastructure.Left.X ($infrastructure.Left.Y - 40) "wires"
Draw-Arrow $shared.Bottom.X $shared.Bottom.Y $presentation.Top.X $presentation.Top.Y "events"

$legendX = 50
$legendY = 1230
$graphics.DrawString("Dependency direction", $groupFont, $darkBrush, $legendX, $legendY)
$graphics.DrawString("Presentation -> Application -> Domain", $textFont, $darkBrush, $legendX, ($legendY + 42))
$graphics.DrawString("Infrastructure -> Application (ports), Bootstrap wires all, Shared stays cross-feature", $textFont, $darkBrush, $legendX, ($legendY + 72))

$bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)

$linePen.Dispose()
$titleFont.Dispose()
$groupFont.Dispose()
$textFont.Dispose()
$smallFont.Dispose()
$darkBrush.Dispose()
$mutedBrush.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Output $outputPath
