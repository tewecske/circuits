{
  description = "Circuits - logic circuit sandbox";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f system);

      # Check these against the Godot docs ("Exporting for Android") for the Godot version in use.
      android = {
        platformVersion = "35";
        buildToolsVersion = "35.0.1";
        ndkVersion = "28.1.13356709";
        cmakeVersion = "3.10.2";
      };
    in
    {
      devShells = forAllSystems (system:
        let
          pkgs = nixpkgs.legacyPackages.${system};
          dotnet = pkgs.dotnet-sdk_10;

          # Core toolchain: build, test, run the editor.
          base = [
            dotnet
            pkgs.godot_4-mono
            pkgs.git
            pkgs.git-lfs
          ];

          env = {
            DOTNET_CLI_TELEMETRY_OPTOUT = "1";
            DOTNET_NOLOGO = "1";
            # Point Godot's C# tooling at our SDK rather than whichever one it was built with.
            DOTNET_ROOT = "${dotnet}/share/dotnet";
          };
        in
        {
          # nix develop
          default = pkgs.mkShell (env // { packages = base; });

          # nix develop .#assets: icons, art and sound editing.
          assets = pkgs.mkShell (env // {
            packages = base ++ [ pkgs.inkscape pkgs.krita pkgs.audacity ];
          });

          # nix develop .#windows: lets Godot set .exe icons and metadata when exporting from Linux.
          windows = pkgs.mkShell (env // {
            packages = base ++ [ pkgs.wineWowPackages.stable ];
          });

          # nix develop .#android: JDK and Android SDK for Godot's Android export.
          # The Android SDK is unfree and needs its licence accepted, so it uses its own nixpkgs instance.
          android =
            let
              pkgsAndroid = import nixpkgs {
                inherit system;
                config = {
                  allowUnfree = true;
                  android_sdk.accept_license = true;
                };
              };
              sdk = pkgsAndroid.androidenv.composeAndroidPackages {
                platformVersions = [ android.platformVersion ];
                buildToolsVersions = [ android.buildToolsVersion ];
                includeNDK = true;
                ndkVersions = [ android.ndkVersion ];
                cmakeVersions = [ android.cmakeVersion ];
                includeEmulator = false;
              };
              sdkRoot = "${sdk.androidsdk}/libexec/android-sdk";
            in
            pkgsAndroid.mkShell (env // {
              packages = base ++ [ pkgsAndroid.jdk17 sdk.androidsdk ];
              ANDROID_HOME = sdkRoot;
              ANDROID_SDK_ROOT = sdkRoot;
              JAVA_HOME = "${pkgsAndroid.jdk17.home}";
              shellHook = ''
                echo "In Godot: Editor Settings > Export > Android, set Android SDK Path to $ANDROID_HOME"
                echo "and Java SDK Path to $JAVA_HOME. Create a debug keystore with keytool if you have none."
              '';
            });
        });
    };
}
