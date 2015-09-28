namespace Grace.Runtime
{
    /// <summary>
    /// A user-space Grace object arising from an object
    /// constructor expression.
    /// </summary>
    public class UserObject : GraceObject
    {
        /// <summary>
        /// Creates a basic user object.
        /// </summary>
        public UserObject()
        {
            SetFlag(Flags.UserspaceObject);
        }
    }
}
