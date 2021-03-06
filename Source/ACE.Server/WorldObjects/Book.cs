using System;
using System.Collections.Generic;

using ACE.Database.Models.Shard;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;

namespace ACE.Server.WorldObjects
{
    public sealed class Book : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Book(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            //SetProperty(PropertyInt.EncumbranceVal, 0);
            //SetProperty(PropertyInt.Value, 0);

            //SetProperty(PropertyBool.IgnoreAuthor, false);
            //SetProperty(PropertyInt.AppraisalPages, 0);
            //SetProperty(PropertyInt.AppraisalMaxPages, 1);

            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Book(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            BaseDescriptionFlags |= ObjectDescriptionFlag.Book;

            SetProperty(PropertyInt.AppraisalPages, Biota.BiotaPropertiesBookPageData.Count);
            SetProperty(PropertyInt.AppraisalMaxPages, Biota.BiotaPropertiesBook.MaxNumPages);
        }

        public void SetProperties(string name, string shortDesc, string inscription, string scribeName, string scribeAccount)
        {
            if (!String.IsNullOrEmpty(name)) SetProperty(PropertyString.Name, name);
            if (!String.IsNullOrEmpty(shortDesc)) SetProperty(PropertyString.ShortDesc, shortDesc);
            if (!String.IsNullOrEmpty(inscription)) SetProperty(PropertyString.Inscription, inscription);
            if (!String.IsNullOrEmpty(scribeName)) SetProperty(PropertyString.ScribeName, scribeName);
            if (!String.IsNullOrEmpty(scribeAccount)) SetProperty(PropertyString.ScribeAccount, scribeAccount);
        }

        public BiotaPropertiesBookPageData AddPage(uint authorId, string authorName, string authorAccount, bool ignoreAuthor, string pageText)
        {
            var pages = Biota.GetBookAllPages(Guid.Full, BiotaDatabaseLock);

            if (pages == null || pages.Count == AppraisalMaxPages)
                return null;

            var page = new BiotaPropertiesBookPageData()
            {
                ObjectId = Biota.Id,
                PageId = (uint)pages.Count,
                AuthorId = authorId,
                AuthorName = authorName,
                IgnoreAuthor = ignoreAuthor,
                PageText = pageText
            };

            Biota.AddBookPage(page, BiotaDatabaseLock, out var alreadyExists);

            if (alreadyExists) return null;

            SetProperty(PropertyInt.AppraisalPages, pages.Count + 1);
            return page;
        }

        public bool ModifyPage(uint pageId, string pageText)
        {
            var page = Biota.GetBookPageData(Guid.Full, pageId, BiotaDatabaseLock);

            if (page == null || page.PageText.Equals(pageText))
                return false;

            page.PageText = pageText;
            ChangesDetected = true;

            return true;
        }

        public bool DeletePage(uint pageId)
        {
            var pages = Biota.GetBookAllPages(Guid.Full, BiotaDatabaseLock);

            var success = Biota.DeleteBookPage(pageId, out var entity, BiotaDatabaseLock);

            if (!success)
                return false;

            if (pageId < pages.Count - 1)
            {
                // handle deleting page from middle of book
                for (var i = pageId + 1; i < pages.Count; i++)
                {
                    var page = Biota.GetBookPageData(Guid.Full, i, BiotaDatabaseLock);
                    page.PageId--;
                }
            }
            SetProperty(PropertyInt.AppraisalPages, pages.Count - 1);
            ChangesDetected = true;
            return true;
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item does not exist in the players possession.<para />
        /// If the item was outside of range, the player will have been commanded to move using DoMoveTo before ActOnUse is called.<para />
        /// When this is called, it should be assumed that the player is within range.
        /// </summary>
        public override void ActOnUse(WorldObject worldObject)
        {
            if (worldObject is Player)
            {
                var player = worldObject as Player;
                BookUseHandler(player.Session);
            }
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item should be in the players possession.
        /// </summary>
        public override void UseItem(Player player)
        {
            BookUseHandler(player.Session);
        }

        /// <summary>
        /// One function to handle both Player.OnUse and Landblock.HandleActionOnUse functions
        /// </summary>
        /// <param name="session"></param>
        private void BookUseHandler(Session session)
        {
            int maxChars = Biota.BiotaPropertiesBook.MaxNumCharsPerPage;
            int maxPages = Biota.BiotaPropertiesBook.MaxNumPages;

            string authorName;
            if (ScribeName != null)
                authorName = ScribeName;
            else
                authorName = "";

            string authorAccount;
            if (ScribeAccount != null)
                authorAccount = ScribeAccount;
            else
                authorAccount = "";

            //uint authorID = ScribeIID ?? 0xFFFFFFFF;
            uint authorID = (ScribeIID.HasValue) ? (uint)ScribeIID : 0xFFFFFFFF;

            List<PageData> pageData = new List<PageData>();
            foreach (var p in Biota.BiotaPropertiesBookPageData)
            {
                PageData newPage = new PageData();
                newPage.AuthorID = p.AuthorId;
                newPage.AuthorName = p.AuthorName;
                newPage.AuthorAccount = p.AuthorAccount;
                newPage.PageText = p.PageText;
                newPage.IgnoreAuthor = p.IgnoreAuthor;
                pageData.Add(newPage);
            }

            bool ignoreAuthor = IgnoreAuthor ?? false;

            string inscription;
            if (Inscription != null)
                inscription = Inscription;
            else
                inscription = "";

            var bookDataResponse = new GameEventBookDataResponse(session, Guid.Full, maxChars, maxPages, pageData, inscription, authorID, authorName, ignoreAuthor);
            session.Network.EnqueueSend(bookDataResponse);

            var sendUseDoneEvent = new GameEventUseDone(session);
            session.Network.EnqueueSend(sendUseDoneEvent);
        }
    }
}
