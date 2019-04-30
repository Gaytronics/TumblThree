﻿using System.Collections.Generic;

using TumblThree.Domain.Models.Blogs;

namespace TumblThree.Applications.Services
{
    public interface ISelectionService
    {
        IList<IBlog> SelectedBlogFiles { get; }

        void AddRange(IEnumerable<IBlog> collection);

        void RemoveRange(IEnumerable<IBlog> collection);
    }
}
