import { Routes } from '@angular/router';
import { loginRedirctGuard, routeGuard } from './components/auth/route-guard';
import { MapView } from './components/pages/mapview/mapview'
import { LoginPage } from './components/pages/login-page/login-page';
import { Overlay } from './components/pages/overlay/overlay';
import { Home } from './components/pages/home/home';

export const routes: Routes = [
    {path: 'login', component: LoginPage, //canDeactivate: [loginRedirctGuard]
    },
    {path: '', component: Overlay, //canActivate: [routeGuard],
        children: [
            {path: 'home', component: Home},

        ]
    }
    
    
];
