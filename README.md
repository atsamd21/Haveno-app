## Haveno Android app

This is the mobile version of Haveno, it uses the gRPC daemon from <a href="https://github.com/haveno-dex/haveno" target="_blank">Haveno</a> but is not endorsed by the Haveno developers.

The APK can be downloaded from <a href="https://github.com/atsamd21/Haveno-app/releases" target="_blank">releases</a> or you can build from source.

> [!note]
> This repository is configured for the public test network/stagenet. You will need to use a third party mainnet network to make real trades. The developers of this repository do not endorse any networks at this time.

***********************************************

![alt text](https://github.com/atsamd21/Haveno-app/blob/master/app-new-ui.png "Image of application")

## About
The app currently only works on Android, however, MAUI also supports iOS so with some work it could support both with the limitation that iOS requires a remote Haveno node.

## App modes
There are two modes

1. Standalone - which installs a rootfs with tor, java 21 and the Haveno daemon on the device, this is a ~400mb download.

2. Remote node - https://github.com/atsamd21/Haveno-remote-node. This requires Orbot with "Power user mode" enabled in settings. Or other Tor SOCKS5 proxy on port 9050. Orbot can be installed either via the Play Store or github as an APK: https://github.com/guardianproject/orbot-android/releases.

## Network operators
Follow the <a href="https://github.com/atsamd21/Haveno-app/blob/master/CONFIGURATION.MD" target="_blank">instructions</a> on how to configure the app for your network.

## Developers
Run the project from <a href="https://visualstudio.microsoft.com/downloads/" target="_blank">Visual Studio 2022</a> community edition.
The app can run either on a physical device with USB debugging and developer mode enabled or via the Android emulator.

You can also use <a href="https://www.jetbrains.com/rider/download/?section=linux" target="_blank">Rider</a> instead of VS2022. However hot reload does not work.

I would assume that both of these IDEs send analytics and other information to their respective companies. VS Code should also work but requires a bit more setup.

Guides for both Visual Studio and VS Code can be found <a href="https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation?view=net-maui-9.0&tabs=visual-studio" target="_blank">here</a>.

Standalone uses proot - https://github.com/atsamd21/proot

## GitHub actions

The app can be built using GitHub actions. Make sure the workflows are enabled and create a tag, this will build the app and create a release.

1. Install a <a href="https://adoptium.net/temurin/releases" target="_blank">Java SDK</a> which includes keytool

2. Create a keystore

    keytool -genkeypair -v -keystore havenoapp.keystore -alias havenoappkey -keyalg RSA -keysize 2048 -validity 10000

3. Convert to base64
    
    base64 -w 0 havenoapp.keystore > keystore.b64

4. On GitHub, go to settings > Secrets and variables > Actions

    Add the following repository secrets:

    | Variable              | Value                                 
    |-----------------------|---------------------------------------
    | KEYSTORE              | The base64 string from the previous step
    | KEYSTORE_ALIAS        | havenoappkey
    | KEYSTORE_PASS         | The password you entered when creating the keystore

5. Create a new tag and push it - this will start the build process and a release will be created automatically

## Build via CLI - WIP

Check out the build files in .github/workflows for the time being

1. Download the latest version of the .NET 9 SDK https://dotnet.microsoft.com/en-us/download/dotnet/9.0

2. In a terminal run "dotnet workload install maui"

Sources: https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-9.0&source=recommendations

## Contribute
If you are not a developer but would like to contribute you can do so by testing the app and reporting bugs <a href="https://github.com/atsamd21/Haveno-app/issues" target="_blank">here</a>.

Make sure to include the make and model of the device as well as android version.

## Donate
```
8AjnVUsDsLTAynhXLZSymreRdvySXmt8vcDU6osXPTGudmazMPvM71h4Y14x4Je2iYHqv6tRUq52zixb5nV9oFwp7Y1DVRU
```
