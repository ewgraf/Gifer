﻿using System;
using System.Diagnostics;
using WixSharp;
using Microsoft.Deployment.WindowsInstaller;

namespace Gifer.MSI {
    public class Program {
        public static void Main(string[] args) {
			// Because of the excessive size of the WiX Toolset the WixSharp.wix.bin NuGet package
			//  isn't a direct dependency of the WixSharp package and it needs to be added to the project explicitly:
			Compiler.WixLocation = @"..\packages\WixSharp.wix.bin.3.11.0\tools\bin";

            string prefix = @"..\gifer\bin\Debug";
			var project = new Project("gifer",
				new Dir(@"%AppData%\gifer",
					new File($@"{prefix}\gifer.exe",
						new FileShortcut("Gifer", @"%AppData%\Microsoft\Windows\Start Menu\Programs")
					)
				),
                new ManagedAction(CustomActions.OpenDefaultAppsWindow)
            );

			project.BackgroundImage = "gifer-background.png";
			project.ControlPanelInfo.Comments = "Gifer is a program for viewing .gif and other images.";
			project.ControlPanelInfo.HelpLink = "https://github.com/ewgraf/gifer";
			project.ControlPanelInfo.Manufacturer = "https://github.com/ewgraf";
			project.ControlPanelInfo.ProductIcon = @"..\gifer\Resources\gifer logo 42x42 exe icon.ico";
			project.GUID = new Guid("56628ce2-91ad-464c-a005-a19e09a5c9a3");
			project.LicenceFile = "licence.rtf";
            project.Version = new Version(3, 0);

            Compiler.BuildMsi(project);
		}

        public static class CustomActions {
            [CustomAction]
            public static ActionResult OpenDefaultAppsWindow(Session session) {
                Process.Start("ComputerDefaults.exe");
                return ActionResult.Success;
            }
        }
    }
}