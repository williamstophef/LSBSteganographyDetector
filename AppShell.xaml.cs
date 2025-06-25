namespace LSBSteganographyDetector
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            
            // Register routes for navigation
            Routing.RegisterRoute("batchresults", typeof(Views.BatchResultsPage));
            Routing.RegisterRoute("stegotools", typeof(Views.SteganographyToolsPage));
        }
    }
} 