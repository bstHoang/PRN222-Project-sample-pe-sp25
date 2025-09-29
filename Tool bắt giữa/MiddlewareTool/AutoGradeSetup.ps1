# AutoGradeSetup.ps1
# Thiết lập cổng mong muốn cho môi trường chấm bài của bạn
$ClientPort = "5000"
$ServerPort = "5001"

function Set-Appsettings {
    param(
        [Parameter(Mandatory=$true)]
        [string]$PublishDirectory, # Đường dẫn thư mục output của dotnet publish
        
        [Parameter(Mandatory=$true)]
        [string]$Port,

        [Parameter(Mandatory=$true)]
        [string]$Role # "Server" hoặc "Client"
    )
    
    $FilePath = Join-Path -Path $PublishDirectory -ChildPath "appsettings.json"

    if ($Role -eq "Server") {
        # Server phải lắng nghe trên 5001
        $ConfigData = @{
            "ServerPort" = $Port
        }
    } else { 
        # Client phải gọi đến 5000 (Proxy của bạn)
        $ConfigData = @{
            "BaseUrl" = "http://localhost:$Port"
        }
    }

    # Chuyển đổi thành JSON và ghi vào file (sử dụng UTF8 để tránh lỗi)
    $ConfigData | ConvertTo-Json -Depth 2 | Out-File $FilePath -Encoding UTF8 -Force
    Write-Host "s [Role: $($Role)] Config written to $FilePath (Port: $Port)" -ForegroundColor Green
}

# Hướng dẫn sử dụng:
# Chạy script này sau khi chạy dotnet publish cho từng sinh viên.
# Ví dụ: Set-Appsettings -PublishDirectory "C:\Grading\SinhVien_A\publish\server" -Port $ServerPort -Role "Server"