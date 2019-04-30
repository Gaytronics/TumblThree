﻿using System.Collections.Generic;
using System.ComponentModel;

namespace TumblThree.Domain.Models.Files
{
    public interface IFiles : INotifyPropertyChanged
    {
        string Name { get; }

        BlogTypes BlogType { get; }

        IList<string> Links { get; }

        void AddFileToDb(string fileName);

        bool CheckIfFileExistsInDB(string url);

        bool Save();

        IFiles Load(string fileLocation);
    }
}
