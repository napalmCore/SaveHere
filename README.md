<p align="center">
  <div align="center">
  <img src="https://github.com/user-attachments/assets/3469dbda-700f-4c4c-9682-420bb6905059" alt="Screenshot of SaveHere App" width="45%"/>
  <img src="https://github.com/user-attachments/assets/8cac9045-7b28-4ec8-b309-2f1f1cd969c1" alt="Screenshot of SaveHere App" width="45%"/>
  </div>
  <br>
</p>


<div align="center">
  <h1>SaveHere</h1>
  <h4>Cloud Download Manager</h4>
  <h6>In v3.0, the app has been rewritten from scratch in .Net Blazor</h6>
  <h6>Ytdlp backend has been added for downloading Video/Audio from Youtube/Soundcloud/...</h6>
  <h6>Enable WarpPlus proxy to bypass Youtube restrictions</h6>
  <h6>Spotify link converter has been added</h6>
  <h6>RJMusic link converter has been added</h6>
  <h6>Media Converter has been added</h6>
  <img >
</div>


## Table of Contents

- [What this app does](#what-does-it-do)
- [Dependencies](#dependencies)
- [How to run this app using docker](#how-to-run-this-app-using-docker)
- [How to run this app using dotnet](#how-to-run-this-app-using-dotnet)
- [To Do](#to-do)
- [How to contribute](#how-to-contribute)
- [Disclaimer](#disclaimer)


## What does it do

SaveHere is a cloud download manager that allows you to download files from either direct links or video/audio from websites like youtube/soundcloud/etc using ytdlp (see [Supported Sites](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md)).


## Dependencies

SaveHere uses .Net Core with Blazor. To run SaveHere, you need to have either `docker` or `dotnet` installed.

To run the app using `dotnet`, you will need to have .Net 8+ installed. Checkout [ms/dotnet](https://dotnet.microsoft.com/en-us/download) or [ms/dotnet-sdk](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). If you're running ubuntu, you can just do `sudo apt install dotnet8`.

In addition, it is recommended that you run SaveHere behind a reverse proxy such as [Nginx](https://nginx.org/) or [Nginx Proxy Manager](https://nginxproxymanager.com/). This will allow you to access the app using your own domain name and SSL certificate, and provide additional security and performance benefits.

_If your IP is restricted by YouTube or other websites, you'll need a proxy to bypass the restrictions. The app has a built-in proxy service that you can use. To activate it, visit the proxy page, click 'Install,' then 'Start,' and wait for the proxy service to connect properly._

The Media Converter component uses [ffmpeg](https://www.ffmpeg.org/) for converting media to other formats. `ffmpeg` is already installed in the `docker` image. But if you're not using the docker image, you need install it yourself.


## How to run this app using docker

To run SaveHere using `docker`, follow these steps:

1. Create a file named `docker-compose.yml` and fill it with this:
```docker
services:
 savehere:
   image: ghcr.io/gudarzi/savehere:latest
   environment:
     - ASPNETCORE_ENVIRONMENT=Production
     - ASPNETCORE_HTTP_PORTS=8080
     #- ASPNETCORE_HTTPS_PORTS=8081
     - SIGNALR_HUB_URL=http://localhost:8080/DownloadProgressHub
     - BASIC_AUTH_USERNAME="" # Provide Basic Auth Username If Required
     - BASIC_AUTH_PASSWORD="" # Provide Basic Auth Password If Required
     - PUID=1000
     - PGID=1000
     - "urls=http://0.0.0.0:8080"
   ports:
     - "172.17.0.1:18480:8080"
     #- "172.17.0.1:18481:8081"
   volumes:
     - ./downloads:/app/downloads # Remove this if you experience very long loading times
     - ./db:/app/db # Remove this if you experience very long loading times
   user: 0:0
```

2. Run the app using this command:
```bash
docker compose pull
docker compose up -d --build --force-recreate
```

3. Now the app is available at `172.17.0.1:18480`. 

_You can use `"0.0.0.0:18480:8080"` to publish the app on all available interfaces._

_If you encounter any errors about write permissions or corrupt database, just remove folders `db` and `downloads` that the app has created._

_If you're running the app behind a basic auth, provide `BASIC_AUTH_USERNAME` and `BASIC_AUTH_PASSWORD` environment variables in the docker-compose configs._


## How to run this app using dotnet

To run SaveHere using `dotnet`, follow these steps:

1. Clone the repository from GitHub and navigate into the directory:
```bash
git clone https://github.com/gudarzi/SaveHere.git
cd SaveHere
```

2. Build the app:
```bash
dotnet build SaveHere/SaveHere/SaveHere.csproj -c Release
```

3. Navigate to your app directory:
```bash
cd SaveHere/SaveHere/bin/Release/net8.0/
```

4. Run the app:
```bash
dotnet SaveHere.dll --urls=http://127.0.0.1:7777
```

Now the app is running at localhost `127.0.0.1` port `7777`.

If you're running the app behind a basic auth, set these 2 environment variables before running the app.

Linux
```bash
export BASIC_AUTH_USERNAME="your_username"
export BASIC_AUTH_PASSWORD="your_password"
```

Windows cmd
```cmd
set BASIC_AUTH_USERNAME=your_username
set BASIC_AUTH_PASSWORD=your_password
```

Windows powershell
```powershel
$env:BASIC_AUTH_USERNAME = "your_username"
$env:BASIC_AUTH_PASSWORD = "your_password"
```

## To Do
- [ ] Add user accounts and set their access policies
- [x] Add youtube downloader
- [x] Add spotify downloader
- [x] Add rjmusic downloader
- [ ] Add terminal
- [ ] Add full file manager
- [x] Add ~~temporary~~ short links
- [x] Check [issues](https://github.com/gudarzi/SaveHere/issues) for more!


## How to contribute

I welcome contributions from the community to help improve SaveHere. If you're interested in contributing, please check the [To Do](#to-do) list or take a look at the project's [issues](https://github.com/gudarzi/SaveHere/issues) page to see if there are any open issues that you can help with. You can also submit pull requests with bug fixes, new features, or other improvements. Before submitting a pull request, please make sure that your code follows the project's coding standards and that all tests are passing. I will review all pull requests as soon as possible and provide feedback if necessary.


## Disclaimer

This is a hobby project that I work on in my spare time. While I try to make it as good as possible, I cannot guarantee that it is free of bugs or errors, or that it will meet your specific needs. As such, I cannot be held responsible for any damage or loss that may result from using this project.

I welcome any contributions that can help improve the project, but I cannot guarantee that I will be able to incorporate all suggested changes or respond to all feedback. I also reserve the right to reject any contributions that I deem inappropriate or not in line with the project's goals.

