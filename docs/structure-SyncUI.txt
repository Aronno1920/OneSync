SyncUI/
├── SyncUI.csproj
├── Platform/
│   ├── Windows/
│   ├── MacCatalyst/
│   └── Android/
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── SyncJobViewModel.cs
│   └── FileMonitorViewModel.cs
├── Views/
│   ├── MainPage.xaml
│   ├── SyncJobPage.xaml
│   └── FileListView.xaml
├── Services/
│   ├── GrpcSyncClient.cs
│   ├── FileSystemWatcherService.cs
│   └── NotificationService.cs
└── Models/
    ├── SyncJob.cs
    └── FileNode.cs