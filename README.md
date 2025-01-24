<p align="center">
  <img src="https://github.com/user-attachments/assets/8cac9045-7b28-4ec8-b309-2f1f1cd969c1" alt="Screenshot of SaveHere App"/>
  <br>
</p>

<div align="center">
  <h1>SaveHere</h1>
  <h4>Cloud Download Manager</h4>
  <h6>In v3.0, the app has been rewritten from scratch in .Net Blazor</h6>
  <h6>Ytdlp backend has been added for downloading Video/Audio from Youtube/Soundcloud/...</h6>
  <img >
</div>


## Table of Contents

- [What this app does](#what-this-app-does)
- [Dependencies](#dependencies)
- [How to run this app](#how-to-run-this-app)
- [To Do](#to-do)
- [How to contribute](#how-to-contribute)
- [Disclaimer](#disclaimer)

## What this app does

SaveHere is a minimal cloud file manager that allows you to download files from the internet and store them on your own server. It uses ASP.NET Core with Blazor.

The app was built to address the issue of downloading large files from slow servers or unstable connections. With SaveHere, you can enter the URL of the file you want to download, and the app will download it to your server. You can then download the file from your own server using your own domain name and URL, with the ability to pause and resume the download as needed.


## Dependencies

To run SaveHere, you will need to have .Net 8+ installed. Checkout [ms/dotnet](https://dotnet.microsoft.com/en-us/download) or [ms/dotnet-sdk](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

If you're running ubuntu, you can just do `sudo apt install dotnet8`.

In addition, it is recommended that you run SaveHere behind a reverse proxy such as [Nginx](https://nginx.org/) or [Nginx Proxy Manager](https://nginxproxymanager.com/). This will allow you to access the app using your own domain name and SSL certificate, and provide additional security and performance benefits.


## How to run this app

To run SaveHere, follow these steps:

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
- [ ] Add youtube downloader
- [ ] Add spotify downloader
- [ ] Add rjmusic downloader
- [ ] Add terminal
- [ ] Add full file manager
- [ ] Add temporary short links
- [x] Check [issues](https://github.com/gudarzi/SaveHere/issues) for more!


## How to contribute

I welcome contributions from the community to help improve SaveHere. If you're interested in contributing, please check the [To Do](#to-do) list or take a look at the project's [issues](https://github.com/gudarzi/SaveHere/issues) page to see if there are any open issues that you can help with. You can also submit pull requests with bug fixes, new features, or other improvements. Before submitting a pull request, please make sure that your code follows the project's coding standards and that all tests are passing. I will review all pull requests as soon as possible and provide feedback if necessary.


## Disclaimer

This is a hobby project that I work on in my spare time. While I try to make it as good as possible, I cannot guarantee that it is free of bugs or errors, or that it will meet your specific needs. As such, I cannot be held responsible for any damage or loss that may result from using this project.

I welcome any contributions that can help improve the project, but I cannot guarantee that I will be able to incorporate all suggested changes or respond to all feedback. I also reserve the right to reject any contributions that I deem inappropriate or not in line with the project's goals.

