#!/bin/bash

# Enable echo of commands
set -e

echo "This script will build and push a Docker image."

echo
echo "About to build the Docker image: ghcr.io/gudarzi/savehere"
echo
read -p "Do you want to proceed with the build? (y/N): " confirm

if [[ $confirm =~ ^[Yy]$ ]]; then
    echo "Building Docker image..."
    if docker build -t ghcr.io/gudarzi/savehere -f ./SaveHere/SaveHere/Dockerfile .; then
        echo "Build successful."
        
        echo
        echo "About to push the Docker image to: ghcr.io/gudarzi/savehere"
        echo
        read -p "Do you want to proceed with the push? (y/N): " confirm_push
        
        if [[ $confirm_push =~ ^[Yy]$ ]]; then
            echo "Pushing Docker image..."
            if docker push ghcr.io/gudarzi/savehere; then
                echo "Push successful."
            else
                echo "Push failed."
                exit 1
            fi
        else
            echo "Push cancelled by user."
        fi
    else
        echo "Build failed. Exiting script."
        exit 1
    fi
else
    echo "Build cancelled by user."
fi

echo
echo "Script finished."