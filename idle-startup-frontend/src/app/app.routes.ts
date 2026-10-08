import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';
import { adminChildGuard, adminMatchGuard, playerGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in — Rich Life',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    title: 'Create account — Rich Life',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    // The admin app — staff only, its own shell, no game. A non-admin does not match this
    // route at all (canMatch, so its code is never downloaded) and falls through to the
    // game below, ending on the dashboard. The API enforces the role independently (403).
    path: 'admin',
    canMatch: [adminMatchGuard],
    canActivateChild: [adminChildGuard],
    loadComponent: () =>
      import('./features/admin/admin-layout.component').then((m) => m.AdminLayoutComponent),
    children: [
      {
        path: '',
        title: 'Overview — Rich Life admin',
        data: { heading: 'Overview', blurb: 'How the game is doing right now.' },
        loadComponent: () =>
          import('./features/admin/overview/admin-overview.component').then((m) => m.AdminOverviewComponent),
      },
      {
        path: 'players',
        title: 'Players — Rich Life admin',
        data: { heading: 'Players', blurb: 'Search accounts; set cash, reset, promote or delete.' },
        loadComponent: () =>
          import('./features/admin/players/admin-players.component').then((m) => m.AdminPlayersComponent),
      },
      {
        path: 'catalogue',
        title: 'Catalogue — Rich Life admin',
        data: { heading: 'Catalogue', blurb: 'Businesses and assets players can buy.' },
        loadComponent: () =>
          import('./features/admin/catalogue/admin-catalogue.component').then((m) => m.AdminCatalogueComponent),
      },
      {
        path: 'managers',
        title: 'Manager names — Rich Life admin',
        data: { heading: 'Manager names', blurb: 'The pool hired managers get their random names from.' },
        loadComponent: () =>
          import('./features/admin/managers/admin-managers.component').then((m) => m.AdminManagersComponent),
      },
    ],
  },
  {
    // Layout persistant (HUD + nav) pour toutes les pages du jeu — joueurs uniquement :
    // playerGuard renvoie un admin vers /admin.
    path: '',
    canActivate: [authGuard, playerGuard],
    loadComponent: () =>
      import('./shared/components/layout/layout.component').then((m) => m.LayoutComponent),
    children: [
      {
        path: 'dashboard',
        title: 'Dashboard — Rich Life',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'businesses',
        title: 'Businesses — Rich Life',
        loadComponent: () =>
          import('./features/businesses/businesses.component').then((m) => m.BusinessesComponent),
      },
      {
        path: 'leaderboard',
        title: 'Leaderboard — Rich Life',
        loadComponent: () =>
          import('./features/leaderboard/leaderboard.component').then(
            (m) => m.LeaderboardComponent,
          ),
      },
      {
        path: 'luxury',
        title: 'Luxury — Rich Life',
        loadComponent: () =>
          import('./features/luxury/luxury.component').then((m) => m.LuxuryComponent),
      },
      {
        path: 'profile',
        title: 'Your profile — Rich Life',
        loadComponent: () =>
          import('./features/profile/profile.component').then((m) => m.ProfileComponent),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
