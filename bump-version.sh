#!/bin/bash

# Usage Examples:
# ./bump-version.sh major
# ./bump-version.sh minor
# ./bump-version.sh patch
# ./bump-version.sh (same as patch)

# Default to patch if no argument provided
VERSION_TYPE=${1:-patch}

# Validate version type
if [[ ! "$VERSION_TYPE" =~ ^(major|minor|patch)$ ]]; then
    echo "Error: Version type must be 'major', 'minor', or 'patch'"
    exit 1
fi

CSPROJ_PATH="SaveHere/SaveHere/SaveHere.csproj"

# Check if file exists
if [ ! -f "$CSPROJ_PATH" ]; then
    echo "Error: Could not find $CSPROJ_PATH"
    exit 1
fi

# Extract current version
CURRENT_VERSION=$(grep -oP '(?<=<Version>)\d+\.\d+\.\d+(?=</Version>)' "$CSPROJ_PATH")

if [ -z "$CURRENT_VERSION" ]; then
    echo "Error: Could not find version in $CSPROJ_PATH"
    exit 1
fi

# Split version into parts
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

# Increment based on type
case $VERSION_TYPE in
    "major")
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        ;;
    "minor")
        MINOR=$((MINOR + 1))
        PATCH=0
        ;;
    "patch")
        PATCH=$((PATCH + 1))
        ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"

# Update the file
sed -i "s/<Version>$CURRENT_VERSION<\/Version>/<Version>$NEW_VERSION<\/Version>/" "$CSPROJ_PATH"

echo "Version bumped to $NEW_VERSION"

# If script was double-clicked or run without arguments, wait for input
if [ -z "$1" ]; then
    echo -e "\nPress ENTER to exit..."
    read -r
fi