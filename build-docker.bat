@echo off

REM echo "YOUR_PERSONAL_ACCESS_TOKEN" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin

echo This script will build and push a Docker image.

echo.
echo About to build the Docker image: ghcr.io/gudarzi/savehere
echo.
set /p "confirm=Do you want to proceed with the build? (y/n): "

if /i "%confirm%"=="y" (
    echo Building Docker image...
    docker build -t ghcr.io/gudarzi/savehere -f .\SaveHere\SaveHere\Dockerfile .

    if errorlevel 1 (
        echo Build failed. Exiting script.
        exit /b 1
    ) else (
        echo Build successful.
    )

    echo.
    echo About to push the Docker image to: ghcr.io/gudarzi/savehere
    echo.
    set /p "confirm=Do you want to proceed with the push? (y/n): "

    if /i "%confirm%"=="y" (
        echo Pushing Docker image...
        docker push ghcr.io/gudarzi/savehere

        if errorlevel 1 (
            echo Push failed.
        ) else (
            echo Push successful.
        )
    ) else (
        echo Push cancelled by user.
    )
) else (
    echo Build cancelled by user.
)

echo.
echo Script finished.
pause