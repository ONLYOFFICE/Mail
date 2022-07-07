﻿namespace ASC.Mail.Models;

public sealed class MailAttachGarbage : MailGarbage
{
    private readonly int _id;
    public override int Id
    {
        get { return _id; }
    }

    private readonly string _path;
    public override string Path
    {
        get { return _path; }
    }

    public MailAttachGarbage(string user, int attachId, string stream, int number, string storedName)
    {
        _id = attachId;
        _path = MailStoragePathCombiner.GetFileKey(user, stream, number, storedName);
    }
}
