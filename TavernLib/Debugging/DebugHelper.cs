namespace TavernLib.Debugging
{
    public class DebugHelper
    {
        private NLogCatcher _logCatcher = new();
        
        
        public void OnGui()
        {
            _logCatcher.OnGui();
        }
    }
}