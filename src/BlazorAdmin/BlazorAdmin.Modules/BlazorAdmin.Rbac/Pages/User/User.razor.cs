﻿using BlazorAdmin.Component.Dialogs;
using BlazorAdmin.Component.Pages;
using BlazorAdmin.Rbac.Pages.User.Dialogs;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace BlazorAdmin.Rbac.Pages.User
{
    public partial class User
    {
        private List<UserModel> Users = new List<UserModel>();

        private int Page = 1;

        private int Size = 10;

        private int Total = 0;

        private string? SearchText;

        private string? SearchRealName;

        private string? SearchRole;

        private MudDataGrid<UserModel> dataGrid = null!;

        private PageDataGridOne PageDataGridOne = new();

        private List<Data.Entities.Rbac.Role> RoleList = new();

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            using var context = await _dbFactory.CreateDbContextAsync();
            RoleList = context.Roles.AsNoTracking().ToList();
        }


        private async Task<GridData<UserModel>> GetTableData(GridState<UserModel> gridState)
        {
            await InitialData();
            return new GridData<UserModel>() { TotalItems = Users.Count, Items = Users };
        }


        private async Task InitialData()
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var searchedUserIdList = new List<int>();
            if (!string.IsNullOrEmpty(SearchRole))
            {
                var role = int.Parse(SearchRole);
                searchedUserIdList = context.UserRoles.Where(ur => ur.RoleId == role)
                    .Select(ur => ur.UserId).Distinct().ToList();
            }

            IQueryable<Data.Entities.Rbac.User> query = context.Users.Where(u => !u.IsDeleted && !u.IsSpecial);
            if (!string.IsNullOrEmpty(SearchText))
            {
                query = query.Where(u => u.Name.Contains(SearchText));
            }
            if (!string.IsNullOrEmpty(SearchRealName))
            {
                query = query.Where(u => u.RealName.Contains(SearchRealName));
            }
            if (searchedUserIdList.Count > 0)
            {
                query = query.Where(u => searchedUserIdList.Contains(u.Id));
            }

            Users = await query.Skip((Page - 1) * Size).Take(Size)
                .Select(p => new UserModel
                {
                    Id = p.Id,
                    Avatar = p.Avatar,
                    Name = p.Name,
                    RealName = p.RealName,
                    IsEnabled = p.IsEnabled,
                }).ToListAsync();
            Total = await query.CountAsync();

            var roles = (from r in context.Roles
                         join ur in context.UserRoles on r.Id equals ur.RoleId
                         select new { r.Name, ur.UserId }).ToList();

            foreach (var user in Users)
            {
                user.Number = (Page - 1) * Size + Users.IndexOf(user) + 1;
                user.Roles = roles.Where(r => r.UserId == user.Id).Select(r => r.Name).ToList();
            }

        }

        private async Task PageChangedClick(int page)
        {
            Page = page;
            await InitialData();
        }

        private async Task AddUserClick()
        {
            var parameters = new DialogParameters { };
            var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraLarge };
            var result = await _dialogService.Show<CreateUserDialog>(Loc["UserPage_CreateNewTitle"], parameters, options).Result;
            if (!result.Canceled)
            {
                await dataGrid.ReloadServerData();
            }
        }

        private async Task DeleteUserClick(int userId)
        {
            await _dialogService.ShowDeleteDialog(Loc["UserPage_DeleteTitle"], null,
            async (e) =>
            {
                using var context = await _dbFactory.CreateDbContextAsync();
                var user = context.Users.Find(userId);
                if (user != null)
                {
                    user.IsDeleted = true;
                    context.Users.Update(user);

                    var urs = context.UserRoles.Where(ur => ur.UserId == userId);
                    context.UserRoles.RemoveRange(urs);

                    await context.SaveChangesAsync();

                    _snackbarService.Add("删除成功！", Severity.Success);
                }
                else
                {
                    _snackbarService.Add("用户信息不存在！", Severity.Error);
                }
                await dataGrid.ReloadServerData();
            });
        }


        private async Task EditUserClick(int userId)
        {
            var parameters = new DialogParameters
            {
                {"UserId",userId }
            };
            var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraLarge };
            var result = await _dialogService.Show<UpdateUserDialog>(Loc["UserPage_EditTitle"], parameters, options).Result;
            if (!result.Canceled)
            {
                await dataGrid.ReloadServerData();
            }
        }

        private async Task ChangePasswordClick(int userId)
        {
            var parameters = new DialogParameters
            {
                {"UserId",userId }
            };
            var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraLarge };
            await _dialogService.Show<ChangePasswordDialog>(Loc["UserPage_ModifyPasswordTitle"], parameters, options).Result;
        }

        private async Task SetUserRoleClick(int userId)
        {
            var parameters = new DialogParameters
            {
                {"UserId",userId }
            };
            var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraLarge };
            var result = await _dialogService.Show<UserRoleDialog>(Loc["UserPage_SetUserRoleTitle"], parameters, options).Result;
            if (!result.Canceled)
            {
                await dataGrid.ReloadServerData();
            }
        }

        private async Task ChangeUserActive(int userId, bool isEnabled)
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var user = context.Users.Find(userId);
            if (user != null)
            {
                user.IsEnabled = isEnabled;
                await context.SaveChangesAsync();
                _snackbarService.Add(Loc["UserPage_StatusChangedMessage"], Severity.Success);
                Users.FirstOrDefault(u => u.Id == userId)!.IsEnabled = isEnabled;
            }
        }


        private void SearchReset()
        {
            SearchText = "";
            SearchRole = "";
            SearchRealName = "";
            Page = 1;
            dataGrid.ReloadServerData();
        }

        private class UserModel
        {
            public int Id { get; set; }

            public int Number { get; set; }

            public string? Avatar { get; set; }

            public string Name { get; set; } = null!;

            public string? RealName { get; set; }

            public bool IsEnabled { get; set; }

            public List<string> Roles { get; set; } = new();
        }
    }
}
