using AppKit;
using Stgadgets.Host.Mac;

NSApplication.Init();
var app = NSApplication.SharedApplication;
app.Delegate = new AppDelegate();
app.Run();
