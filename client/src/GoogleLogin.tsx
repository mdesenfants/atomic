import * as hello from 'hellojs';
import * as React from 'react';
import { AtomicCounter } from './atomicCounter';
import './GoogleLogin.css';

hello.init({
    google: '1076081007580-rabmg87rit0dcdcc6m29pecc35i0lj5p.apps.googleusercontent.com'
});

export default class GoogleLogin extends React.Component {
    private static async getGoogleToken(): Promise<string|null> {
        const value = await hello('google').login({
            force: false,
            response_type: 'id_token token',
            scope: 'openid'
        });

        return value.authResponse ? value.authResponse.id_token ? value.authResponse.id_token : null : null;
    }

    public render() {
        return (
            <button onClick={this.signIn}>Sign In</button>
        );
    }

    private async signIn(): Promise<void> {
        const goog = await GoogleLogin.getGoogleToken();

        if (!goog) {
            return;
        }

        const counter = new AtomicCounter(await AtomicCounter.getAuthToken(goog));

        // await counter.createTenant();
        await Promise.all([
            await counter.increment(),
            await counter.increment(),
        ]);

        alert(await counter.count());
    }
}