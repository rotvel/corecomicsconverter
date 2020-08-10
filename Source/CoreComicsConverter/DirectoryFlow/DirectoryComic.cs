﻿using CoreComicsConverter.Model;
using System.Collections.Generic;

namespace CoreComicsConverter.DirectoryFlow
{
    public class DirectoryComic : Comic
    {
        public DirectoryComic(string directory, string[] files) : base(ComicType.Directory, directory)
        {
            Files = files;

            OutputDirectory = directory;

            OutputFile = $"{System.IO.Path.TrimEndingDirectorySeparator(directory)}.cbz";
        }

        public string[] Files { get; private set; }

        public static List<DirectoryComic> List(string directory, string[] files)
        {
            return new List<DirectoryComic> { new DirectoryComic(directory, files) };
        }
    }
}
