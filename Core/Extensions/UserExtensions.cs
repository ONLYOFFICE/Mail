namespace ASC.Mail.Core.Extensions
{
    public static class UserExtensions
    {
        public static bool IsVisitor(this UserInfo ui, UserManager UserManager)
        {
            return ui != null && UserManager.IsUserInGroup(ui.Id, ASC.Core.Users.Constants.GroupEveryone.ID);
        }
    }
}
