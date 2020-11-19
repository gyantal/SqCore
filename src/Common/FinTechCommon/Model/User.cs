using System;
using System.Diagnostics;

namespace FinTechCommon
{
    [DebuggerDisplay("Username = {Username}, Email({Email})")]
    public class User
    {
        public int Id { get; set; } = -1;
        public string Username { get; set; } = String.Empty;
        public string Password { get; set; } = String.Empty;
        public string Title { get; set; } = String.Empty;
        public string Firstname { get; set; } = String.Empty;
        public string Lastname { get; set; } = String.Empty;
        public string Email { get; set; } = String.Empty;

        public string Fullname { get { return $"{Title} {Firstname} {Lastname}"; } }
        public string Initials { get { return $"{Firstname[0]}{Lastname[0]}"; } } 
    }

    public class UserInDb	// for quick JSON deserialization. In DB the fields has short names, and not all Asset fields are in the DB anyway
    {
        public int id { get; set; } = -1;
        public string username { get; set; } = String.Empty;
        public string password { get; set; } = String.Empty;
        public string title { get; set; } = String.Empty;
        public string firstname { get; set; } = String.Empty;
        public string lastname { get; set; } = String.Empty;
        public string email { get; set; } = String.Empty;
    }

}