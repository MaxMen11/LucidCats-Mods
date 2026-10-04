{
	description = "Lucid Cats modding dev shells";

	inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

	outputs = { self, nixpkgs }:
		let
			systems = [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];
			forAllSystems = nixpkgs.lib.genAttrs systems;
		in
		{
			devShells = forAllSystems (system:
				let
					pkgs = nixpkgs.legacyPackages.${system};
				in
				{
					default = pkgs.mkShell {
						packages = [ pkgs.dotnetCorePackages.sdk_8_0 ];
					};
				} // nixpkgs.lib.optionalAttrs pkgs.stdenv.isLinux {
					native = pkgs.mkShell {
						packages = [
							pkgs.dotnetCorePackages.sdk_8_0
							pkgs.cmake
							pkgs.gnumake
							pkgs.pkgsCross.mingwW64.stdenv.cc
						];
					};
				});
		};
}
