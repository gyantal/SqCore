using System;
using System.Diagnostics;

namespace FinTechCommon
{
    [DebuggerDisplay("Username = {Username}, Email({Email})")]
    public class User
    {
        public int Id { get; set; } = -1;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Firstname { get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string Fullname { get { return $"{Title} {Firstname} {Lastname}"; } }
        public string Initials { get { return $"{Firstname[0]}{Lastname[0]}"; } }

        // The == Operator compares the reference identity while the Equals() method compares only contents.
        // The default implementation of Equals supports reference equality for reference types, and bitwise equality for value types. 
        // So, for classes, it checks for reference unless you override equals.
        // .ToLookup(r => r.User); // ToLookup() uses User.Equals()
        // but userPointer1 == userPointer2 doesn't use Equals()
        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) 
                return false;
            else
                return DeepEquals((User)obj);
        }

        public bool DeepEquals(User user)
        {
            return Id.Equals(user.Id)   // we have the option to say it is equal if ID matches. But probably, even if Id is unique, better to compare all content
                   && Username.Equals(user.Username)
                   && Password == user.Password
                   && Title == user.Title
                   && Firstname == user.Firstname
                   && Lastname == user.Lastname
                   && ((Email == null) ? user.Email == null: Email.Equals(user.Email));  // email can be null
        }

        // https://stackoverflow.com/questions/6470059/warning-overrides-object-equalsobject-o-but-does-not-override-object-get
        public override int GetHashCode()   // if overrides Object.Equals(object o), you are supposed to override Object.GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    public class UserInDb	// for quick JSON deserialization. In DB the fields has short names, and not all Asset fields are in the DB anyway
    {
        public int id { get; set; } = -1;
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string firstname { get; set; } = string.Empty;
        public string lastname { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
    }

}