using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Fin.Base;
using SqCommon;

namespace Fin.MemDb;

public class UserInDb // for quick JSON deserialization. In DB the fields has short names, and not all Asset fields are in the DB anyway
{
    public int Id { get; set; } = -1;
    public string Name { get; set; } = string.Empty;
    public string Pwd { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public int Isadmin { get; set; } = 0;
    public string Visibleusers { get; set; } = string.Empty;
}

// All Users in gSheet: https://docs.google.com/spreadsheets/d/10fc451YcdIQxtAHI_clCzYmZxdbGzzd5xZq67oKj0H8/edit#gid=0
// Initially, data came from PostgresSql table, but we don't use PostgresSql now.
// There is no programmed mechanism to crawl that to Redis.sq_user, so changes has to be manually added both in gSheet + Redis
[DebuggerDisplay("Username = {Username}, Email({Email})")]
public class User
{
    public int Id { get; set; } = -1;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public bool IsHuman { get; set; } = false; // AllUser, SqBacktester... users are not humans.
    public bool IsAdmin { get; set; } = false;
    public User[] VisibleUsers { get; set; } = Array.Empty<User>(); // array is faster than List, https://stackoverflow.com/questions/454916/performance-of-arrays-vs-lists

    public string Fullname { get { return $"{Title} {Firstname} {Lastname}"; } }
    public string Initials { get { return $"{Firstname[0]}{Lastname[0]}"; } }

    public User(UserInDb usrInDb)
    {
        Id = usrInDb.Id;
        Username = usrInDb.Name;
        Password = usrInDb.Pwd;
        Email = usrInDb.Email;
        Title = usrInDb.Title;
        Firstname = usrInDb.Firstname;
        Lastname = usrInDb.Lastname;
        IsHuman = usrInDb.Id > 30;    // IDs smaller than 30 are reserved for parts of the system.
        IsAdmin = usrInDb.Isadmin == 1;
    }

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
        return Id.Equals(user.Id) // we have the option to say it is equal if ID matches. But probably, even if Id is unique, better to compare all content
                && Username.Equals(user.Username)
                && Password == user.Password
                && Title == user.Title
                && Firstname == user.Firstname
                && Lastname == user.Lastname
                && ((Email == null) ? user.Email == null : Email.Equals(user.Email));  // email can be null
    }

    // https://stackoverflow.com/questions/6470059/warning-overrides-object-equalsobject-o-but-does-not-override-object-get
    public override int GetHashCode() // if overrides Object.Equals(object o), you are supposed to override Object.GetHashCode()
    {
        return Id.GetHashCode();
    }

    public List<BrokerNav> GetBrokerNavsOrdered(List<BrokerNav> allNavs)
    {
        // Add AggNav first, so it is the first in the list.
        List<BrokerNav> userNavs = allNavs.Where(r => r.User == this).ToList();
        for (int i = 1; i < userNavs.Count; i++) // if index 0 is the AggregatedNav, then we don't have to do anythin
        {
            if (userNavs[i].IsAggregatedNav) // if aggNav is not the first one
            {
                userNavs.MoveItemAtIndexToFront(i);
                break;
            }
        }
        return userNavs;
    }

    public List<BrokerNav> GetAllVisibleBrokerNavsOrdered() // ordered, so the user's own BrokerNavs are the first. If there is aggregatedNav, that is the first.
    {
        // SelectableNavs is an ordered list of tickers. The first item is user specific. User should be able to select between the NAVs. DB, Main, Aggregate.
        // bool isAdmin = UserEmail == Utils.Configuration["Emails:Gyant"].ToLower();
        // if (isAdmin) // Now, it is not used. Now, every Google email user with an email can see DC NAVs. Another option is that only Admin users (GA,BL,LN) can see the DC user NAVs.
        TsDateData<SqDateOnly, uint, float, uint> histData = MemDb.gMemDb.DailyHist.GetDataDirect(); // only add navs which has history. Otherwise, there is no point to show to user, and it will crash.
        List<BrokerNav> allNavsWithHistory = MemDb.gMemDb.AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV && histData.Data.ContainsKey(r.AssetId)).Select(r => (BrokerNav)r).ToList();

        List<BrokerNav> visibleNavs = new();

        User firstNavUser = this; // the first NAV in the list should be the logged user's own NAVs.
        List<BrokerNav> firstNavs = firstNavUser.GetBrokerNavsOrdered(allNavsWithHistory);
        if (firstNavs.Count == 0) // if the logged user doesn't have any NAVS, the default fallback is the DC user as first user
        {
            firstNavUser = MemDb.gMemDb.Users.Where(r => r.Username == "drcharmat").FirstOrDefault()!;
            firstNavs = firstNavUser.GetBrokerNavsOrdered(allNavsWithHistory);
        }
        visibleNavs.AddRange(firstNavs);    // First add the current user NAVs. Virtual Aggregated should come first.

        User[] addUsers = IsAdmin ? MemDb.gMemDb.Users : VisibleUsers;             // then the NAVs of other users
        foreach (var user in addUsers)
        {
            if (user != firstNavUser) // was already added in step 1
                visibleNavs.AddRange(user.GetBrokerNavsOrdered(allNavsWithHistory));
        }

        return visibleNavs;
    }
}