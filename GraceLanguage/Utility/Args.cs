using System;
using System.Collections.Generic;
using System.Linq;
using Grace.Runtime;
using System.IO;
using Grace.Parsing;
using Grace.Execution;

namespace Grace.Utility
{
    /// <summary>
    /// Static methods for accessing arguments
    /// </summary>
    public class UnusedArguments
    {

        /// <summary>
        /// a list of unused command line arguments
        /// </summary>
        public static List<string> UnusedArgs = new List<string>();
    }
}

