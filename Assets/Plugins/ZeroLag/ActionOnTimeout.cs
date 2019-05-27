namespace ZeroLag
{
    /// <summary>
    /// Defines what to do when authoritive server received a command way to far in the past.
    /// Executing commands beyond PredictiveModel's threshold is not allowed.
    /// </summary>
    public enum ActionOnTimeout
    {
        /// <summary>
        /// Execute command later.
        /// </summary>
        ExecuteLater,
        /// <summary>
        /// Cancel command completely.
        /// </summary>
        Cancel
    }
}