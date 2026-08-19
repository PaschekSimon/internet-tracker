# Internet-Tracker

Internet-Tracker is a personal Repository for various Tools that help track the statistics of various ZTE Routers

The Software runs on my DreiTube (MF282+) on Firmware Version `CR_H3GATMF282PLUSV1.0.0B23` and claude says it should also run on these devices:
`ZTE MF283/MF283+, MF286/MF286A/MF286D, MF287/MF287+/MF287Pro, MF288, MF289F/MF289D, MF823/MF823D, MF910, MF920, and MF79`


## Taskbar-Monitor
Run the **[build.bat](./taskbar-monitor/build.bat)** file and run the **RouterTaskbarMonitor.exe**

A new icon should appear in the Windows System Tray where you right-click it and select "Settings"

Here you enter your Router Admin Password so that the App can pull the stats from the Router

## Web-Dashboard

1. add a .env file with your Router Password in Root Folder
2. `cd web-dashboard`
3. `npm run dev`

## Contributing

You can create a Pull request and an underlying Issue would be great

## License

[MIT](https://opensource.org/license/MIT)

