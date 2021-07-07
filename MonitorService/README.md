# Windows Service Commands

**Create a Windows Service**
```
# Create a Windows Service
sc create NetworkMonitor DisplayName="Network Monitor" binPath="C:\MonitorService.exe"

# Start a Windows Service
sc start NetworkMonitor

# Stop a Windows Service
sc stop NetworkMonitor

# Delete a Windows Service
sc delete NetworkMonitor

```

Once the service is created you can see it in the services tab in Task Manager (Ctrl + Shift + Esc)
where you can also start/stop it.

Open the EVent Viewer > Application Logs to find the service logs
