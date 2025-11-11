#### This guide assumes that you have already set up Haveno for your network.
***********************************************

1.  Merge https://github.com/haveno-dex/haveno/commit/fc788c6f646fdfb6bf97493d0368101709867174.

    This will allow GitHub actions to automatically build the daemon when a tag is created. 

2. Fork the haveno-app repository and open the Manta.csproj file in a text editor.

    | Property              | Value                                 
    |-----------------------|---------------------------------------
    | ApplicationTitle      | App name shown to the user
    | ApplicationId         | Change this to com.[SOMETHING], this is the app id that Android uses and not shown to the user
    | DaemonUrl             | Url of where the daemon.jar files are hosted. If you're using GitHub actions, use the url of the release that was created
    | Network               | Change this to XMR_MAINNET
    | HavenoAppName         | Change this to haveno-[SOMETHING], this works the same as the app data directory on desktop
    | MauiIcon              | This is the app icon, you will need to update the SVG icon in Resources/AppIcon, make sure it is named appicon.svg
    | MauiSplashScreen      | This is the splash screen image shown when the app starts, update the SVG in Resources/Splash

***********************************************

![alt text](https://github.com/atsamd21/Haveno-app/blob/master/AppImages/CHANGETHESE1.png "Change these 1")

3. Open the AppConstants.cs file in a text editor and change the ApplicationId string so that it matches the one in Manta.csproj.

   You will also need to change MakerFeePct and TakerFeePct fields so that they match the maker and taker fee percentages in the desktop app.

4. Configure remote Haveno node - https://github.com/atsamd21/Haveno-remote-node

#### Updating the app

You will need to create a new release/url if updating the daemon since the app will compare the last installed url with the one set in Manta.csproj. However, you cannot update just because Haveno updates, there may be breaking changes and so the mobile app will need to be updated to support these changes first.

When the code for the app has been updated to support the latest Haveno version you can build the daemon again and create a new release. Update the url value for DaemonUrl in Manta.csproj. Then the app can be built. Once installed on the device it will automatically fetch the new daemon and update.