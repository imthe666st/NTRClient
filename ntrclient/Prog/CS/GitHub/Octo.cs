﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using System;
using System.Windows.Forms;
using ntrclient.Extra;

namespace ntrclient.Prog.CS.GitHub
{
    internal class Octo
    {
        public static GitHubClient Git;
        public static IRepositoriesClient Rep;

        public static Release LastRelease;

        public static void Init()
        {
            Git = new GitHubClient(new ProductHeaderValue("ntrclient"));
            Rep = Git.Repository;
        }

        public static async Task<Release> GetLastUpdate()
        {
            try {
                IReadOnlyList<Release> lastReleases = await Rep.Release.GetAll("imthe666st", "ntrclient");
                LastRelease = lastReleases[0];
                return LastRelease;
            //} catch ( TaskCanceledException e)
            //{
                
            } catch ( Exception e )
            {
                if (e == new TaskCanceledException())
                {
                    
                }
                MessageBox.Show("An error occured trying to look for updates!");
                BugReporter br = new BugReporter(e, "Updater exception", false);
                return null;
            }
        }

        public static string GetLastVersionName()
        {
            return LastRelease != null ? LastRelease.TagName : "ERROR";
        }

        public static string GetLastVersionBody()
        {
            return LastRelease != null ? LastRelease.Body : "ERROR";
        }
    }
}
